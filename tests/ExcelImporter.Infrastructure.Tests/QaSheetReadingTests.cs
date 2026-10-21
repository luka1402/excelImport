using System.Diagnostics;
using ExcelImporter.Application.Files;
using ExcelImporter.Infrastructure.Excel;
using Microsoft.Extensions.Options;
using Shouldly;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>QA (COA-33): scenariji nad test fajlovima iz T08 koje razvojni testovi ne pokrivaju.</summary>
public class QaSheetReadingTests
{
    private static readonly string TestDataDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly ExcelDataSheetReader reader = new(Options.Create(new ExcelReadingOptions()));

    private static FileStream Open(string relativePath) => File.OpenRead(Path.Combine(TestDataDirectory, relativePath));

    [Theory]
    [InlineData("samples/01-valid.xlsx")]
    [InlineData("samples/02-with-errors.xlsx")]
    [InlineData("samples/03-empty-cells.xlsx")]
    [InlineData("samples/04-multi-sheet.xlsx")]
    [InlineData("samples/05-header-not-first-row.xlsx")]
    [InlineData("samples/07-cyrillic-special.xlsx")]
    [InlineData("samples/08-demodb-gradovi.xlsx")]
    [InlineData("samples/09-demodb-kupci.xlsx")]
    [InlineData("samples/10-demodb-kupci-invalid.xlsx")]
    [InlineData("samples/11-demodb-proizvodi.xlsx")]
    public void EverySample_ListsSheetsAndReadsRectangularPreviewOfAtMostTwentyRows(string file)
    {
        var analyzer = new FileAnalyzer(new SingleFileStore(Path.Combine(TestDataDirectory, file)), reader);

        var analysis = analyzer.Analyze(Guid.NewGuid(), null, TestContext.Current.CancellationToken).ShouldNotBeNull();

        analysis.Sheets.ShouldNotBeEmpty();
        analysis.Preview.Rows.Count.ShouldBeLessThanOrEqualTo(FileAnalyzer.PreviewRowCount);
        analysis.Preview.Rows.Select(row => row.Count).Distinct().Count().ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public void EmptySheet_ReturnsEmptyPreview_WithoutError()
    {
        using var content = Open("samples/04-multi-sheet.xlsx");

        var preview = reader.ReadPreview(content, 2, 20, TestContext.Current.CancellationToken);

        preview.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void HiddenSheet_CanBeRead_WhenSelectedExplicitly_ButIsNotDefault()
    {
        var analyzer = new FileAnalyzer(new SingleFileStore(Path.Combine(TestDataDirectory, "samples/04-multi-sheet.xlsx")), reader);

        var byDefault = analyzer.Analyze(Guid.NewGuid(), null, TestContext.Current.CancellationToken).ShouldNotBeNull();
        var hidden = analyzer.Analyze(Guid.NewGuid(), 4, TestContext.Current.CancellationToken).ShouldNotBeNull();

        byDefault.SelectedSheet.IsHidden.ShouldBeFalse();
        hidden.SelectedSheet.Name.ShouldBe("Skriven");
    }

    [Fact]
    public void NegativeSheetIndex_ThrowsSheetNotFound()
    {
        var analyzer = new FileAnalyzer(new SingleFileStore(Path.Combine(TestDataDirectory, "samples/01-valid.xlsx")), reader);

        Should.Throw<SheetNotFoundException>(() => analyzer.Analyze(Guid.NewGuid(), -1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WideHeader_IsCappedAtDefaultMaxPreviewColumns()
    {
        using var content = Open("malicious/m11-wide-header-1000-columns.xlsx");

        var preview = reader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);

        preview.ColumnsTruncated.ShouldBeTrue();
        preview.Rows.ShouldAllBe(row => row.Count == 256);
    }

    [Fact]
    public void ZipBomb_IsHandledQuickly_WithoutExhaustingMemory_OrFailsWithDomainException()
    {
        using var content = Open("malicious/m08-zip-bomb.xlsx");
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            reader.ListSheets(content, TestContext.Current.CancellationToken);
            content.Position = 0;
            reader.ReadPreview(content, 0, 20, TestContext.Current.CancellationToken);
        }
        catch (UnreadableExcelFileException)
        {
            // dozvoljen ishod
        }

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        (GC.GetTotalAllocatedBytes(precise: true) - before).ShouldBeLessThan(500_000_000);
    }

    [Theory]
    [InlineData("malicious/m06-valid-xlsx-wrong-extension.txt")]
    [InlineData("malicious/m07-valid-xlsx-wrong-extension.exe")]
    public void ValidXlsxWithWrongExtension_IsReadByContent(string file)
    {
        using var content = Open(file);

        reader.ListSheets(content, TestContext.Current.CancellationToken).ShouldNotBeEmpty();
    }

    [Fact]
    public void LargeFile_PreviewStaysWithinMemoryAndTimeBudget()
    {
        const string largeFile = "large/06-large-100k.xlsx";
        Assert.SkipUnless(File.Exists(Path.Combine(TestDataDirectory, largeFile)), "Fajl 06-large-100k.xlsx nije generisan.");

        GC.Collect();
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        var peak = baseline;
        var stopwatch = Stopwatch.StartNew();

        using var content = Open(largeFile);
        var sheets = reader.ListSheets(content, TestContext.Current.CancellationToken);
        peak = Math.Max(peak, GC.GetTotalMemory(false));
        content.Position = 0;
        var preview = reader.ReadPreview(content, 0, FileAnalyzer.PreviewRowCount, TestContext.Current.CancellationToken);
        peak = Math.Max(peak, GC.GetTotalMemory(false));

        sheets.ShouldNotBeEmpty();
        preview.Rows.Count.ShouldBe(20);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
        // Pregled 20 redova iz 100k fajla (5 MB na disku) ne sme da drži desetine MB živih objekata.
        (peak - baseline).ShouldBeLessThan(100_000_000);
    }

    private sealed class SingleFileStore(string path) : IUploadedFileStore
    {
        public Stream? OpenRead(Guid fileId) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}
