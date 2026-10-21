using ExcelImporter.Application.Files;
using ExcelImporter.Application.Import;
using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class DryRunServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly IReadOnlyList<string?> Header = ["Sifra", "Cena", "Kolicina"];

    private static readonly TableSchema Proizvodi = new(
        "dbo",
        "Proizvodi",
        [
            new ColumnSchema("Id", 1, "int", false, null, 10, 0, true, false),
            new ColumnSchema("Sifra", 2, "nvarchar", false, 10, 0, 0, false, false),
            new ColumnSchema("Cena", 3, "decimal", true, null, 10, 2, false, false),
            new ColumnSchema("Kolicina", 4, "int", false, null, 10, 0, false, false),
            new ColumnSchema("Status", 5, "nvarchar", false, 20, 0, 0, false, false),
        ],
        ["Id"],
        []);

    private static readonly IReadOnlyList<ColumnMapping> Mappings =
        [new("Sifra", "Sifra"), new("Cena", "Cena"), new("Kolicina", "Kolicina")];

    private static readonly IReadOnlyList<ColumnConstant> StatusConstant =
        [new("Status", ConstantValue.FromLiteral("Aktivan"))];

    private static IReadOnlyList<string?> Row(string? sifra, string? cena, string? kolicina) => [sifra, cena, kolicina];

    private static DryRunService CreateService(IEnumerable<IReadOnlyList<string?>>? rows, out FakeReader reader)
    {
        var store = Substitute.For<IUploadedFileStore>();
        store.OpenRead(Arg.Any<Guid>()).Returns(_ => rows is null ? null : new MemoryStream());

        reader = new FakeReader(rows ?? []);

        var schemaProvider = Substitute.For<ISchemaProvider>();
        schemaProvider.GetSchemaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DatabaseSchema([Proizvodi]));

        var lookupLoader = Substitute.For<ILookupDictionaryLoader>();
        return new DryRunService(store, reader, schemaProvider, lookupLoader, TimeProvider.System);
    }

    private static DryRunRequest Request(
        int headerRowIndex = 0,
        IReadOnlyList<ColumnMapping>? mappings = null,
        IReadOnlyList<Rule>? rules = null) =>
        new(Guid.NewGuid(), null, headerRowIndex, "dbo", "Proizvodi", mappings ?? Mappings, StatusConstant, rules ?? [], "cs");

    [Fact]
    public async Task Counts_imported_skipped_and_failed_rows()
    {
        var service = CreateService(
            [
                Header,
                Row("A-1", "10.5", "3"),
                Row("A-2", "7", "0"),
                Row("A-3", "abc", "2"),
                Row("A-4", null, "5"),
            ],
            out _);
        var filter = new RowFilterRule { Action = RowFilterAction.Include, Conditions = [new Condition("Kolicina", ComparisonOperator.Gt, "0")] };

        var result = (await service.RunAsync(Request(rules: [filter]), Ct)).ShouldNotBeNull();

        result.ImportCount.ShouldBe(2);
        result.SkippedCount.ShouldBe(1);
        result.FailedRowCount.ShouldBe(1);
        result.ErrorCount.ShouldBe(1);
        result.Errors.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            error => error.RowNumber.ShouldBe(4),
            error => error.Column.ShouldBe("Cena"),
            error => error.Reason.ShouldNotBeNullOrWhiteSpace());
    }

    [Fact]
    public async Task Row_numbers_are_excel_rows_when_header_is_not_first()
    {
        var service = CreateService(
            [
                [],
                ["Naslov", null, null],
                Header,
                Row("A-1", "1", "1"),
                Row("A-2", "x", "1"),
            ],
            out _);

        var result = (await service.RunAsync(Request(headerRowIndex: 2), Ct)).ShouldNotBeNull();

        result.ImportCount.ShouldBe(1);
        result.Errors.Single().RowNumber.ShouldBe(5);
    }

    [Fact]
    public async Task Preview_contains_first_twenty_imported_rows_with_constants_applied()
    {
        var rows = new List<IReadOnlyList<string?>> { Header };
        rows.AddRange(Enumerable.Range(1, 50).Select(index => Row($"S-{index}", "1.5", "1")));
        var service = CreateService(rows, out _);

        var result = (await service.RunAsync(Request(), Ct)).ShouldNotBeNull();

        result.ImportCount.ShouldBe(50);
        result.PreviewRows.Count.ShouldBe(DryRunService.PreviewRowCount);
        result.PreviewRows[0].RowNumber.ShouldBe(2);
        result.PreviewRows[0].Values["Sifra"].ShouldBe("S-1");
        result.PreviewRows[0].Values["Cena"].ShouldBe(1.5m);
        result.PreviewRows[0].Values["Status"].ShouldBe("Aktivan");
        result.PreviewRows.ShouldNotContain(row => row.Values.ContainsKey("Id"));
    }

    [Fact]
    public async Task Error_list_is_capped_but_total_count_is_exact()
    {
        var rows = new List<IReadOnlyList<string?>> { Header };
        rows.AddRange(Enumerable.Range(1, 250).Select(_ => Row("A", "nije broj", "1")));
        var service = CreateService(rows, out _);

        var result = (await service.RunAsync(Request(), Ct)).ShouldNotBeNull();

        result.FailedRowCount.ShouldBe(250);
        result.ErrorCount.ShouldBe(250);
        result.Errors.Count.ShouldBe(DryRunService.MaxReportedErrors);
        result.ErrorsTruncated.ShouldBeTrue();
        result.Errors[0].RowNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Row_with_several_bad_cells_reports_each_column()
    {
        var service = CreateService([Header, Row("A", "x", "y")], out _);

        var result = (await service.RunAsync(Request(), Ct)).ShouldNotBeNull();

        result.FailedRowCount.ShouldBe(1);
        result.ErrorCount.ShouldBe(2);
        result.Errors.Select(error => error.Column).ShouldBe(["Cena", "Kolicina"], ignoreOrder: true);
    }

    [Fact]
    public async Task Completely_empty_rows_are_ignored()
    {
        var service = CreateService([Header, Row("A", "1", "1"), [], Row(null, " ", null), Row("B", "2", "2")], out _);

        var result = (await service.RunAsync(Request(), Ct)).ShouldNotBeNull();

        result.ImportCount.ShouldBe(2);
        result.FailedRowCount.ShouldBe(0);
        result.EmptyRowCount.ShouldBe(2);
    }

    [Fact]
    public async Task Short_row_missing_trailing_cells_is_treated_as_empty_cells()
    {
        var service = CreateService([Header, ["A"]], out _);

        var result = (await service.RunAsync(Request(), Ct)).ShouldNotBeNull();

        result.FailedRowCount.ShouldBe(1);
        result.Errors.ShouldHaveSingleItem().Column.ShouldBe("Kolicina");
    }

    [Fact]
    public async Task Returns_null_when_file_does_not_exist()
    {
        var service = CreateService(null, out _);

        (await service.RunAsync(Request(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Unknown_source_column_is_a_configuration_error_before_any_row_is_processed()
    {
        var service = CreateService([Header, Row("A", "1", "1")], out _);

        var exception = await Should.ThrowAsync<DryRunConfigurationException>(
            () => service.RunAsync(Request(mappings: [new("Nepostojeca", "Sifra")]), Ct));

        exception.Errors.ShouldContain(error => error.Message.Contains("Nepostojeca"));
    }

    [Fact]
    public async Task Mapping_to_identity_column_is_rejected()
    {
        var service = CreateService([Header, Row("A", "1", "1")], out _);

        await Should.ThrowAsync<DryRunConfigurationException>(
            () => service.RunAsync(Request(mappings: [new("Sifra", "Id")]), Ct));
    }

    [Fact]
    public async Task Missing_header_row_throws()
    {
        var service = CreateService([Header], out _);

        await Should.ThrowAsync<InvalidHeaderRowException>(() => service.RunAsync(Request(headerRowIndex: 5), Ct));
    }

    [Fact]
    public async Task Cancellation_stops_the_pass()
    {
        var rows = new List<IReadOnlyList<string?>> { Header };
        rows.AddRange(Enumerable.Range(1, 10).Select(_ => Row("A", "1", "1")));
        var service = CreateService(rows, out _);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.RunAsync(Request(), cts.Token));
    }

    private sealed class FakeReader(IEnumerable<IReadOnlyList<string?>> rows) : IExcelSheetReader
    {
public IEnumerable<IReadOnlyList<string?>> StreamRows(Stream content, Func<IReadOnlyList<SheetInfo>, SheetInfo> selectSheet, CancellationToken cancellationToken)
        {

            return rows;
        }

        public IReadOnlyList<SheetInfo> ListSheets(Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();

        public SheetPreview ReadPreview(Stream content, int sheetIndex, int rowCount, CancellationToken cancellationToken) => throw new NotSupportedException();

        public WorkbookPreview ReadWorkbook(Stream content, Func<IReadOnlyList<SheetInfo>, SheetInfo> selectSheet, int rowCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
