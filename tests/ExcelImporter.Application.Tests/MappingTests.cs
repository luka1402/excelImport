using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Columns;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class MappingTests
{
    private static ColumnSchema Column(
        string name, string dataType = "nvarchar", bool nullable = true, int? maxLength = 50, bool identity = false, bool computed = false) =>
        new(name, 1, dataType, nullable, maxLength, 0, 0, identity, computed);

    private static ColumnProfile Profile(int index, string name, ColumnType type = ColumnType.Text, bool isEmpty = false, bool isMixed = false) =>
        new(index, name, type, isEmpty, isMixed, 10);

    private static TableSchema Table(params ColumnSchema[] columns) => new("dbo", "Customers", columns, [], []);

    [Theory]
    [InlineData("Šifra Kupca", "sifrakupca")]
    [InlineData("  sifra_kupca ", "sifrakupca")]
    [InlineData("ĐORĐE-Ćosić", "dordecosic")]
    [InlineData("Žaba.Č", "zabac")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_IgnoresCaseWhitespaceAndDiacritics(string? input, string expected) =>
        ColumnNameNormalizer.Normalize(input).ShouldBe(expected);

    [Fact]
    public void Suggest_ExactMatchIgnoringCaseSpacesAndDiacritics()
    {
        var table = Table(Column("SifraKupca"), Column("Email"));
        var suggestions = MappingSuggester.Suggest([Profile(0, "Šifra Kupca"), Profile(1, "EMAIL")], table);

        suggestions.ShouldBe([
            new MappingSuggestion("Šifra Kupca", "SifraKupca", MatchKind.Exact),
            new MappingSuggestion("EMAIL", "Email", MatchKind.Exact)]);
    }

    [Fact]
    public void Suggest_SimilarNameGivesSimilarMatch()
    {
        var table = Table(Column("PhoneNumber"));
        var suggestions = MappingSuggester.Suggest([Profile(0, "Phone Numbr")], table);

        suggestions.ShouldHaveSingleItem().Match.ShouldBe(MatchKind.Similar);
    }

    [Fact]
    public void Suggest_UnrelatedNamesGiveNoSuggestion() =>
        MappingSuggester.Suggest([Profile(0, "Napomena")], Table(Column("Email"))).ShouldBeEmpty();

    [Fact]
    public void Suggest_NamesDifferingOnlyInDigitsAreNotSimilar() =>
        MappingSuggester.Suggest([Profile(0, "Adresa1")], Table(Column("Adresa2"))).ShouldBeEmpty();

    [Fact]
    public void Suggest_ExactWinsOverSimilar_AndTargetIsUsedOnce()
    {
        var table = Table(Column("Name"));
        var suggestions = MappingSuggester.Suggest([Profile(0, "Names"), Profile(1, "name")], table);

        suggestions.ShouldHaveSingleItem().SourceColumn.ShouldBe("name");
    }

    [Fact]
    public void Suggest_SkipsIdentityAndComputedColumns()
    {
        var table = Table(Column("Id", "int", false, null, identity: true), Column("Total", "int", computed: true));
        MappingSuggester.Suggest([Profile(0, "Id"), Profile(1, "Total")], table).ShouldBeEmpty();
    }

    [Fact]
    public void Warnings_TypeMismatch()
    {
        var table = Table(Column("Quantity", "int", maxLength: null), Column("Note"));
        var warnings = MappingWarnings.ForStructure(
            [Profile(0, "Qty", ColumnType.Text), Profile(1, "Note", ColumnType.WholeNumber)],
            table,
            [new ColumnMapping("Qty", "Quantity"), new ColumnMapping("Note", "Note")]);

        var warning = warnings.ShouldHaveSingleItem();
        warning.Kind.ShouldBe(MappingWarningKind.TypeMismatch);
        warning.TargetColumn.ShouldBe("Quantity");
    }

    [Theory]
    [InlineData(ColumnType.DecimalNumber, "int", true)]
    [InlineData(ColumnType.WholeNumber, "decimal", false)]
    [InlineData(ColumnType.Date, "datetime2", false)]
    [InlineData(ColumnType.Date, "bit", true)]
    [InlineData(ColumnType.Boolean, "bit", false)]
    [InlineData(ColumnType.Text, "uniqueidentifier", false)]
    public void Warnings_TypeCompatibilityMatrix(ColumnType source, string target, bool expectWarning)
    {
        var warnings = MappingWarnings.ForStructure(
            [Profile(0, "A", source)], Table(Column("B", target, maxLength: null)), [new ColumnMapping("A", "B")]);

        (warnings.Count == 1).ShouldBe(expectWarning);
    }

    [Fact]
    public void Warnings_EmptySourceColumnNeverMismatches() =>
        MappingWarnings.ForStructure(
            [Profile(0, "A", isEmpty: true)], Table(Column("B", "int", maxLength: null)), [new ColumnMapping("A", "B")])
            .ShouldBeEmpty();

    [Fact]
    public void Warnings_MixedColumnToNumberMismatches() =>
        MappingWarnings.ForStructure(
            [Profile(0, "A", isMixed: true)], Table(Column("B", "int", maxLength: null)), [new ColumnMapping("A", "B")])
            .ShouldHaveSingleItem().Kind.ShouldBe(MappingWarningKind.TypeMismatch);

    [Fact]
    public void Warnings_UnmappedRequiredColumn_ButNotIdentityComputedNullableOrFilledByRule()
    {
        var table = Table(
            Column("Id", "int", false, null, identity: true),
            Column("Hash", "int", false, null, computed: true),
            Column("Optional", nullable: true),
            Column("Country", nullable: false),
            Column("Name", nullable: false),
            Column("Email", nullable: false));

        var warnings = MappingWarnings.ForStructure(
            [Profile(0, "Name")], table, [new ColumnMapping("Name", "Name")], columnsFilledByRules: ["country"]);

        var warning = warnings.ShouldHaveSingleItem();
        warning.Kind.ShouldBe(MappingWarningKind.RequiredColumnUnmapped);
        warning.TargetColumn.ShouldBe("Email");
        warning.SourceColumn.ShouldBeNull();
    }

    [Fact]
    public void TextLength_ReportsCountFirstRowAndLongest_AcrossAllRows()
    {
        var table = Table(Column("Name", maxLength: 5), Column("Code", maxLength: 3));
        IReadOnlyList<string?>[] rows =
        [
            ["abc", "ab"],
            ["abcdef", "abcd"],
            [null, "x"],
            ["abcdefghij", "abc"],
        ];

        var warnings = MappingWarnings.ForTextLength(
            [Profile(0, "Name"), Profile(1, "Code")],
            table,
            [new ColumnMapping("Name", "Name"), new ColumnMapping("Code", "Code")],
            rows,
            firstRowNumber: 2);

        warnings.Count.ShouldBe(2);
        var name = warnings.Single(warning => warning.TargetColumn == "Name");
        name.Kind.ShouldBe(MappingWarningKind.TextTooLong);
        name.AffectedRowCount.ShouldBe(2);
        name.FirstAffectedRow.ShouldBe(3);
        name.LongestValueLength.ShouldBe(10);
        warnings.Single(warning => warning.TargetColumn == "Code").AffectedRowCount.ShouldBe(1);
    }

    [Fact]
    public void TextLength_IgnoresMaxColumnsNonTextColumnsAndFittingValues()
    {
        var table = Table(Column("Unlimited", maxLength: null), Column("Number", "int", maxLength: null), Column("Ok", maxLength: 10));
        IReadOnlyList<string?>[] rows = [[new string('x', 5000), "123456789012345", "short"]];

        MappingWarnings.ForTextLength(
            [Profile(0, "A"), Profile(1, "B"), Profile(2, "C")],
            table,
            [new ColumnMapping("A", "Unlimited"), new ColumnMapping("B", "Number"), new ColumnMapping("C", "Ok")],
            rows,
            2).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_AcceptsValidMapping()
    {
        var schema = new DatabaseSchema([Table(Column("Name"), Column("Email"))]);
        var result = MappingValidator.Validate("dbo", "customers", [new ColumnMapping("Ime", "name")], schema, ["Ime"]);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsNonexistentTargetColumn()
    {
        var schema = new DatabaseSchema([Table(Column("Name"))]);
        var result = MappingValidator.Validate("dbo", "Customers", [new ColumnMapping("A", "Name]; DROP TABLE x;--")], schema, ["A"]);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("ne postoji");
    }

    [Fact]
    public void Validate_RejectsUnknownTable()
    {
        var schema = new DatabaseSchema([Table(Column("Name"))]);

        MappingValidator.Validate("sys", "objects", [new ColumnMapping("A", "Name")], schema, ["A"]).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_RejectsIdentityDuplicateTargetAndMissingSource()
    {
        var schema = new DatabaseSchema([Table(Column("Id", "int", false, null, identity: true), Column("Name"))]);
        var result = MappingValidator.Validate(
            "dbo",
            "Customers",
            [new ColumnMapping("A", "Id"), new ColumnMapping("A", "Name"), new ColumnMapping("B", "NAME"), new ColumnMapping("Nema", "Name")],
            schema,
            ["A", "B"]);

        result.Errors.Count.ShouldBe(4);
        result.Errors.ShouldContain(error => error.Message.Contains("identity"));
        result.Errors.ShouldContain(error => error.Message.Contains("više puta"));
        result.Errors.ShouldContain(error => error.Message.Contains("ne postoji u fajlu"));
    }
}
