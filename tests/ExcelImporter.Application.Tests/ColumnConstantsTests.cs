using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class ColumnConstantsTests
{
    private static ColumnSchema Column(string name, string dataType = "nvarchar", bool nullable = true, int? maxLength = 50, bool identity = false, bool computed = false) =>
        new(name, 1, dataType, nullable, maxLength, 0, 0, identity, computed);

    private static readonly TableSchema _table = new("dbo", "Customers",
    [
        Column("Id", "int", false, null, identity: true),
        Column("Name"),
        Column("Status", maxLength: 10),
        Column("DatumUvoza", "datetime2", false, null),
        Column("Datum", "date", true, null),
        Column("Broj", "int", true, null),
        Column("Total", "int", true, null, computed: true),
    ], [], []);

    private static readonly DateTime _now = new(2026, 10, 21, 14, 30, 0);

    private static IReadOnlyList<MappingError> Validate(string column, ConstantValue value, params string[] mapped) =>
        ColumnConstants.Validate(_table, [new ColumnConstant(column, value)], mapped);

    [Fact]
    public void Validate_LiteralAndDynamicForUnmappedColumns_AreValid()
    {
        var errors = ColumnConstants.Validate(_table,
            [new("Status", ConstantValue.FromLiteral("Aktivan")), new("DatumUvoza", ConstantValue.FromDynamic(DynamicConstantKind.Today))],
            ["Name"]);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_TodayIsComputedFromProvidedMoment_NowKeepsTime()
    {
        var constants = new ColumnConstant[]
        {
            new("Status", ConstantValue.FromLiteral("Aktivan")),
            new("DatumUvoza", ConstantValue.FromDynamic(DynamicConstantKind.Now)),
            new("Datum", ConstantValue.FromDynamic(DynamicConstantKind.Today)),
        };

        var first = ColumnConstants.Resolve(_table, constants, _now);
        var later = ColumnConstants.Resolve(_table, constants, _now.AddDays(1));

        first["Status"].ShouldBe("Aktivan");
        first["DatumUvoza"].ShouldBe(_now);
        first["Datum"].ShouldBe(new DateTime(2026, 10, 21));
        later["Datum"].ShouldBe(new DateTime(2026, 10, 22));
    }

    [Theory]
    [InlineData("Broj", "abc")]
    [InlineData("Datum", "nije datum")]
    [InlineData("Status", "predugacak tekst")]
    [InlineData("DatumUvoza", "")]
    public void Validate_LiteralOfWrongType_IsRejected(string column, string literal) =>
        Validate(column, ConstantValue.FromLiteral(literal)).ShouldHaveSingleItem().TargetColumn.ShouldBe(column);

    [Fact]
    public void Validate_DynamicOnNonDateColumn_IsRejected() =>
        Validate("Broj", ConstantValue.FromDynamic(DynamicConstantKind.Today)).ShouldHaveSingleItem();

    [Theory]
    [InlineData("Nepostojeca")]
    [InlineData("Id")]
    [InlineData("Total")]
    public void Validate_UnknownIdentityOrComputedColumn_IsRejected(string column) =>
        Validate(column, ConstantValue.FromLiteral("1")).ShouldHaveSingleItem();

    [Fact]
    public void Validate_ColumnMappedFromFile_IsRejected() =>
        Validate("Name", ConstantValue.FromLiteral("x"), "name").ShouldHaveSingleItem();

    [Fact]
    public void Validate_DuplicateConstant_IsRejected()
    {
        var errors = ColumnConstants.Validate(_table,
            [new("Status", ConstantValue.FromLiteral("A")), new("status", ConstantValue.FromLiteral("B"))], []);

        errors.ShouldHaveSingleItem();
    }

    [Fact]
    public void Validate_NeitherOrBothLiteralAndDynamic_IsRejected()
    {
        Validate("Status", new ConstantValue()).ShouldHaveSingleItem();
        Validate("Datum", new ConstantValue { Literal = "2026-01-01", Dynamic = DynamicConstantKind.Today }).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Resolve_ValuesPassThroughRowPipelineIntoRow()
    {
        var constants = new ColumnConstant[]
        {
            new("Status", ConstantValue.FromLiteral("Aktivan")),
            new("DatumUvoza", ConstantValue.FromDynamic(DynamicConstantKind.Today)),
        };
        var pipeline = await RowPipeline.CreateAsync([], _table.Columns, "unused", Substitute.For<ILookupDictionaryLoader>(),CancellationToken.None);
        var row = new Dictionary<string, object?>(ColumnConstants.Resolve(_table, constants, _now)) { ["Name"] = "Pera" };

        var outcome = pipeline.Process(row, 2);

        outcome.Kind.ShouldBe(RowOutcomeKind.Imported);
        outcome.Values["Status"].ShouldBe("Aktivan");
        outcome.Values["DatumUvoza"].ShouldBe(new DateTime(2026, 10, 21));
    }
}
