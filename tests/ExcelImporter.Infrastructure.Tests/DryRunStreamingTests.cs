using System.IO.Compression;
using System.Text;
using ExcelImporter.Application.Files;
using ExcelImporter.Application.Import;
using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Infrastructure.Excel;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>Dry run nad pravim .xlsx čitačem: tačni brojevi i 100.000 redova bez držanja fajla u memoriji.</summary>
public class DryRunStreamingTests
{
    private const int RowCount = 100_000;

    private static readonly TableSchema Table = new(
        "dbo",
        "Stavke",
        [
            new ColumnSchema("Naziv", 1, "nvarchar", false, 50, 0, 0, false, false),
            new ColumnSchema("Kolicina", 2, "int", false, null, 10, 0, false, false),
        ],
        [],
        []);

    private readonly ExcelDataSheetReader reader = new(Options.Create(new ExcelReadingOptions()));

    private DryRunService CreateService(string path)
    {
        var store = Substitute.For<IUploadedFileStore>();
        store.OpenRead(Arg.Any<Guid>()).Returns(_ => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        var schemaProvider = Substitute.For<ISchemaProvider>();
        schemaProvider.GetSchemaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new DatabaseSchema([Table]));
        return new DryRunService(store, reader, schemaProvider, Substitute.For<ILookupDictionaryLoader>(), TimeProvider.System);
    }

    private static DryRunRequest Request() =>
        new(Guid.NewGuid(), null, 0, "dbo", "Stavke", [new("Naziv", "Naziv"), new("Kolicina", "Kolicina")], [], [], "cs");

    [Fact]
    public async Task Hundred_thousand_rows_are_validated_with_exact_counts_and_bounded_memory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dryrun-{Guid.NewGuid():N}.xlsx");
        try
        {
            WriteWorkbook(path);
            var service = CreateService(path);

            var baseline = GC.GetTotalMemory(forceFullCollection: true);
            var result = (await service.RunAsync(Request(), TestContext.Current.CancellationToken)).ShouldNotBeNull();
            var peakGrowth = SampledPeakGrowth(path, baseline);

            // Svaki 1000. red ima tekst umesto broja u koloni Kolicina.
            result.FailedRowCount.ShouldBe(RowCount / 1000);
            result.ImportCount.ShouldBe(RowCount - (RowCount / 1000));
            result.SkippedCount.ShouldBe(0);
            result.ErrorCount.ShouldBe(RowCount / 1000);
            result.Errors.Count.ShouldBe(DryRunService.MaxReportedErrors);
            result.Errors[0].ShouldBe(new DryRunError(1001, "Kolicina", result.Errors[0].Reason));
            result.PreviewRows.Count.ShouldBe(DryRunService.PreviewRowCount);

            // Živi objekti tokom prolaza ostaju daleko ispod veličine materijalizovanih 100.000 redova.
            peakGrowth.ShouldBeLessThan(20L * 1024 * 1024);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Prolazi kroz isti fajl čitačem i na svakih 10.000 redova meri žive bajtove (posle punog GC-a).
    private long SampledPeakGrowth(string path, long baseline)
    {
        long peak = 0;
        var seen = 0;
        using var content = File.OpenRead(path);
        foreach (var _ in reader.StreamRows(content, sheets => sheets[0], CancellationToken.None))
        {
            if (++seen % 10_000 == 0)
            {
                peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: true) - baseline);
            }
        }

        seen.ShouldBe(RowCount + 1);
        return peak;
    }

    // Minimalan .xlsx sa shared strings (najgori slučaj za razrešavanje po grupi): kolona A je jedinstven tekst po redu.
    private static void WriteWorkbook(string path)
    {
        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
            <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
            <Default Extension="xml" ContentType="application/xml"/>
            <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
            </Types>
            """);
        Add(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            <sheets><sheet name="Stavke" sheetId="1" r:id="rId1"/></sheets></workbook>
            """);
        Add(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);

        // Indeks 0 = "Naziv", 1 = "Kolicina", 2.. = "Stavka {n}".
        using (var strings = new StreamWriter(zip.CreateEntry("xl/sharedStrings.xml").Open(), new UTF8Encoding(false)))
        {
            strings.Write("""<?xml version="1.0" encoding="UTF-8"?><sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
            strings.Write("<si><t>Naziv</t></si><si><t>Kolicina</t></si>");
            for (var row = 1; row <= RowCount; row++)
            {
                strings.Write($"<si><t>Stavka {row}</t></si>");
            }

            strings.Write("</sst>");
        }

        using var sheet = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(), new UTF8Encoding(false));
        sheet.Write("""<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        sheet.Write("""<row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c></row>""");
        for (var row = 1; row <= RowCount; row++)
        {
            var excelRow = row + 1;
            var quantity = row % 1000 == 0
                ? $"""<c r="B{excelRow}" t="inlineStr"><is><t>mnogo</t></is></c>"""
                : $"""<c r="B{excelRow}"><v>{row % 50}</v></c>""";
            sheet.Write($"""<row r="{excelRow}"><c r="A{excelRow}" t="s"><v>{row + 1}</v></c>{quantity}</row>""");
        }

        sheet.Write("</sheetData></worksheet>");
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
