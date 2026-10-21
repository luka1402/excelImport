using ExcelImporter.Application.Files;
using ExcelImporter.Infrastructure.Excel;
using Microsoft.Extensions.Options;
using Shouldly;

namespace ExcelImporter.Infrastructure.Tests;

public class ExcelDataSheetReaderTests
{
    private static readonly string TestDataDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly ExcelDataSheetReader reader = new(Options.Create(new ExcelReadingOptions()));

    private static FileStream Open(string relativePath) => File.OpenRead(Path.Combine(TestDataDirectory, relativePath));

    [Fact]
    public void ListSheets_ReturnsAllSheetsIncludingEmptyAndHidden()
    {
        using var content = Open("samples/04-multi-sheet.xlsx");

        var sheets = reader.ListSheets(content, TestContext.Current.CancellationToken);

        sheets.Select(sheet => sheet.Name).ShouldBe(["Gradovi", "Kupci", "Prazan", "Proizvodi", "Skriven"]);
        sheets.Select(sheet => sheet.Index).ShouldBe([0, 1, 2, 3, 4]);
        sheets.Where(sheet => sheet.IsHidden).Select(sheet => sheet.Name).ShouldBe(["Skriven"]);
    }

    [Fact]
    public void ReadPreview_ReturnsOnlyFirstTwentyRows_WhenSheetIsLonger()
    {
        using var content = Open("samples/01-valid.xlsx");

        var preview = reader.ReadPreview(content, 0, FileAnalyzer.PreviewRowCount, TestContext.Current.CancellationToken);

        preview.Rows.Count.ShouldBe(20);
        preview.Rows[0].ShouldBe(["Id", "Ime", "Prezime", "Email", "Plata", "DatumZaposlenja", "Aktivan"]);
        preview.Rows.ShouldAllBe(row => row.Count == 7);
    }

    [Fact]
    public void ReadPreview_SelectsRequestedSheet()
    {
        using var content = Open("samples/04-multi-sheet.xlsx");

        var gradovi = reader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);
        content.Position = 0;
        var proizvodi = reader.ReadPreview(content, 3, 20, TestContext.Current.CancellationToken);

        proizvodi.Rows.ShouldNotBeEmpty();
        proizvodi.Rows[0].ShouldNotBe(gradovi.Rows[0]);
    }

    [Fact]
    public void ReadPreview_ThrowsSheetNotFound_WhenIndexIsOutOfRange()
    {
        using var content = Open("samples/04-multi-sheet.xlsx");

        Should.Throw<SheetNotFoundException>(() => reader.ReadPreview(content, 99, 20, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReadPreview_KeepsCyrillicAndSpecialCharactersIntact()
    {
        using var content = Open("samples/07-cyrillic-special.xlsx");

        var sheets = reader.ListSheets(content, TestContext.Current.CancellationToken);
        content.Position = 0;
        var preview = reader.ReadPreview(content, 0, 100, TestContext.Current.CancellationToken);
        var cells = preview.Rows.SelectMany(row => row).OfType<string>().ToList();

        sheets[0].Name.ShouldBe("Подаци");
        cells.ShouldContain(cell => cell.Contains('đ') && cell.Contains('č') && cell.Contains('ć') && cell.Contains('ž') && cell.Contains('š'));
        cells.ShouldContain(cell => cell.Contains("<tag>") && cell.Contains('&'));
        cells.ShouldContain(cell => cell.Contains("=1+1"));
    }

    [Fact]
    public void ReadPreview_KeepsLeadingRowsBeforeHeader()
    {
        using var content = Open("samples/05-header-not-first-row.xlsx");

        var preview = reader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);

        preview.Rows.Count.ShouldBeGreaterThanOrEqualTo(4);
        preview.Rows[0].ShouldContain(cell => cell != null);
        preview.Rows[3].Count(cell => cell != null).ShouldBeGreaterThan(1);
    }

    [Fact]
    public void ReadPreview_TruncatesColumns_WhenHeaderIsTooWide()
    {
        var narrowReader = new ExcelDataSheetReader(Options.Create(new ExcelReadingOptions { MaxPreviewColumns = 100 }));
        using var content = Open("malicious/m11-wide-header-1000-columns.xlsx");

        var preview = narrowReader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);

        preview.ColumnsTruncated.ShouldBeTrue();
        preview.Rows.ShouldAllBe(row => row.Count == 100);
    }

    [Fact]
    public void ReadPreview_DoesNotLoadWholeLargeFile()
    {
        const string largeFile = "large/06-large-100k.xlsx";
        // Veliki fajl se ne drži u repou; generiše ga tests/TestData/generator (--large).
        Assert.SkipUnless(File.Exists(Path.Combine(TestDataDirectory, largeFile)), "Fajl 06-large-100k.xlsx nije generisan.");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Zagrevanje (JIT, statički keševi) da ne ulazi u merenje.
        AllocatedBy(largeFile, rowCount: 1, cancellationToken);

        var oneRow = AllocatedBy(largeFile, rowCount: 1, cancellationToken);
        var twentyRows = AllocatedBy(largeFile, FileAnalyzer.PreviewRowCount, cancellationToken);

        // Redovi se čitaju lazy: cena zavisi od broja traženih redova, a ne od veličine sheet-a.
        // (Konstantni deo je tabela shared strings, koju ExcelDataReader učitava; njen rast ograničava T09.)
        (twentyRows - oneRow).ShouldBeLessThan(2_000_000);
    }

    [Fact]
    public void Analyze_OpensLargeFileOnce_AndAllocatesFarLessThanFileSizeImplies()
    {
        const string largeFile = "large/06-large-100k.xlsx";
        Assert.SkipUnless(File.Exists(Path.Combine(TestDataDirectory, largeFile)), "Fajl 06-large-100k.xlsx nije generisan.");
        var store = new CountingStore(Path.Combine(TestDataDirectory, largeFile));
        var analyzer = new FileAnalyzer(store, reader);
        var cancellationToken = TestContext.Current.CancellationToken;
        analyzer.Analyze(Guid.NewGuid(), null, cancellationToken);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var analysis = analyzer.Analyze(Guid.NewGuid(), null, cancellationToken);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Current.SendDiagnosticMessage($"Analyze 100k: {allocated / 1_000_000.0:F2} MB, {stopwatch.ElapsedMilliseconds} ms");

        analysis.ShouldNotBeNull().Preview.Rows.Count.ShouldBe(FileAnalyzer.PreviewRowCount);
        store.Opened.ShouldBe(2); // jedno otvaranje po Analyze pozivu (zagrevanje + merenje)
        allocated.ShouldBeLessThan(10_000_000);
    }

    private sealed class CountingStore(string path) : IUploadedFileStore
    {
        public int Opened { get; private set; }

        public Stream? OpenRead(Guid fileId)
        {
            Opened++;
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }

    private long AllocatedBy(string file, int rowCount, CancellationToken cancellationToken)
    {
        using var content = Open(file);
        var before = GC.GetAllocatedBytesForCurrentThread();
        reader.ReadPreview(content, 0, rowCount, cancellationToken);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Theory]
    [InlineData("malicious/m01-xlsx-renamed-from-txt.xlsx")]
    [InlineData("malicious/m02-not-a-zip-png-header.xlsx")]
    [InlineData("malicious/m03-zip-magic-truncated.xlsx")]
    [InlineData("malicious/m04-empty-0-bytes.xlsx")]
    [InlineData("malicious/m05-script-disguised.xlsx")]
    public void ListSheets_ThrowsUnreadable_ForNonXlsxContent(string file)
    {
        using var content = Open(file);

        Should.Throw<UnreadableExcelFileException>(() => reader.ListSheets(content, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReadPreview_DoesNotLeakExternalEntityContent_FromDoctype()
    {
        using var content = Open("malicious/m10-xxe-doctype.xlsx");

        string[] outcome;
        try
        {
            var preview = reader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);
            outcome = [.. preview.Rows.SelectMany(row => row).OfType<string>()];
        }
        catch (UnreadableExcelFileException)
        {
            return;
        }

        outcome.ShouldNotContain(cell => cell.Contains("[fonts]", StringComparison.OrdinalIgnoreCase) || cell.Contains("for 16-bit app support", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReadPreview_ReturnsSqlInjectionTextAsPlainData()
    {
        using var content = Open("malicious/m09-sql-injection.xlsx");

        var preview = reader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);

        preview.Rows.SelectMany(row => row).OfType<string>().ShouldContain(cell => cell.Contains("DROP TABLE"));
    }

    [Fact]
    public void ReadPreview_FormatsValuesWithInvariantCulture()
    {
        using var content = Open("samples/01-valid.xlsx");

        var preview = reader.ReadPreview(content, 0, 3, TestContext.Current.CancellationToken);
        var dataRow = preview.Rows[1];

        dataRow[4].ShouldNotBeNull().ShouldNotContain(",");
        dataRow[5].ShouldNotBeNull().ShouldMatch(@"^\d{4}-\d{2}-\d{2}");
        dataRow[6].ShouldBeOneOf("true", "false");
    }
}
