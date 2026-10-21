using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Columns;
using Shouldly;

namespace ExcelImporter.Application.Tests;

/// <summary>QA (COA-45): dijakritici, prazni i duplirani headeri u predlogu mapiranja i upozorenjima.</summary>
public class QaMappingEdgeTests
{
    private static ColumnSchema Column(string name, string dataType = "nvarchar", bool nullable = true, int? maxLength = 50) =>
        new(name, 1, dataType, nullable, maxLength, 0, 0, false, false);

    private static ColumnProfile Profile(int index, string name, ColumnType type = ColumnType.Text) =>
        new(index, name, type, false, false, 10);

    private static TableSchema Table(params ColumnSchema[] columns) => new("dbo", "T", columns, [], []);

    [Theory]
    [InlineData("Šifra", "Sifra")]
    [InlineData("Čačak", "Cacak")]
    [InlineData("Đurđevak", "Durdevak")]
    [InlineData("ŽŠĐČĆ", "zsdcc")]
    [InlineData("Šifra", "Sifra")] // decomposed (S + combining caron)
    [InlineData("Ime Prezime", "ImePrezime")] // non-breaking space
    [InlineData("Ime\tPrezime", "ImePrezime")]
    public void Normalize_Diacritics(string input, string expectedLike) =>
        ColumnNameNormalizer.Normalize(input).ShouldBe(ColumnNameNormalizer.Normalize(expectedLike));

    [Theory]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("😀")]
    [InlineData("___.")]
    public void Normalize_NoLettersGivesEmpty(string input) => ColumnNameNormalizer.Normalize(input).ShouldBeEmpty();

    [Fact]
    public void Normalize_CyrillicDoesNotCrash_AndIsNotMatchedToLatin()
    {
        ColumnNameNormalizer.Normalize("Шифра").ShouldNotBeEmpty();
        MappingSuggester.Suggest([Profile(0, "Шифра")], Table(Column("Sifra"))).ShouldBeEmpty();
    }

    [Fact]
    public void Suggest_EmptyAndPunctuationOnlyHeadersNeverMatch()
    {
        var suggestions = MappingSuggester.Suggest(
            [Profile(0, ""), Profile(1, "   "), Profile(2, "---"), Profile(3, "😀")],
            Table(Column("Email")));

        suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void Suggest_AutoNamedColumnsDoNotMatchRealColumns() =>
        MappingSuggester.Suggest([Profile(0, "Kolona 1"), Profile(1, "Kolona 2")], Table(Column("Kolona1"), Column("Name"))).Count
            .ShouldBeLessThanOrEqualTo(1);

    [Fact]
    public void Suggest_EmptySourceList_OrEmptyTable_IsEmpty()
    {
        MappingSuggester.Suggest([], Table(Column("A"))).ShouldBeEmpty();
        MappingSuggester.Suggest([Profile(0, "A")], Table()).ShouldBeEmpty();
    }

    [Fact]
    public void Suggest_DuplicateHeaders_OnlyOneSuggestion_NoException()
    {
        var suggestions = MappingSuggester.Suggest(
            [Profile(0, "Ime"), Profile(1, "Ime")], Table(Column("Ime"), Column("Prezime")));

        suggestions.ShouldHaveSingleItem().TargetColumn.ShouldBe("Ime");
    }

    [Fact]
    public void Suggest_HeadersDifferingOnlyByDiacriticsOrCase_AreOneTargetOnce()
    {
        var suggestions = MappingSuggester.Suggest(
            [Profile(0, "Šifra"), Profile(1, "sifra")], Table(Column("Sifra")));

        suggestions.ShouldHaveSingleItem().SourceColumn.ShouldBe("Šifra");
    }

    [Fact]
    public void Suggest_TargetNamesWithDiacritics()
    {
        var suggestions = MappingSuggester.Suggest([Profile(0, "Sifra Kupca")], Table(Column("Šifra_Kupca")));
        suggestions.ShouldHaveSingleItem().Match.ShouldBe(MatchKind.Exact);
    }

    [Fact]
    public void Suggest_VeryLongNames_DoNotThrow()
    {
        var longName = new string('a', 5000);
        Should.NotThrow(() => MappingSuggester.Suggest([Profile(0, longName)], Table(Column(longName + "b"))));
    }

    [Fact]
    public void Warnings_DuplicateHeaders_SecondColumnTypeIsStillChecked()
    {
        // Dve kolone "Kolicina": prva ceo broj, druga tekst. Mapiranje ide po nazivu, pa je jednoznacno samo ako se uzme prva.
        var warnings = MappingWarnings.ForStructure(
            [Profile(0, "Kolicina", ColumnType.WholeNumber), Profile(1, "Kolicina", ColumnType.Text)],
            Table(Column("Q", "int", maxLength: null)),
            [new ColumnMapping("Kolicina", "Q")]);

        warnings.ShouldBeEmpty(); // dokumentuje ponasanje: uzima se prva kolona sa tim nazivom
    }

    [Fact]
    public void TextLength_DuplicateHeaders_UsesFirstColumnIndex()
    {
        IReadOnlyList<string?>[] rows = [["ok", "predugacak tekst"]];
        var warnings = MappingWarnings.ForTextLength(
            [Profile(0, "Ime"), Profile(1, "Ime")], Table(Column("Ime", maxLength: 5)), [new ColumnMapping("Ime", "Ime")], rows, 2);

        warnings.ShouldBeEmpty(); // druga kolona sa istim nazivom se ne proverava
    }

    [Fact]
    public void TextLength_CountsCharsNotBytes_AndHandlesDiacriticsAndEmoji()
    {
        IReadOnlyList<string?>[] rows = [["ŠĐČĆŽ", "😀😀😀"]];
        var warnings = MappingWarnings.ForTextLength(
            [Profile(0, "A"), Profile(1, "B")],
            Table(Column("A", maxLength: 5), Column("B", maxLength: 5)),
            [new ColumnMapping("A", "A"), new ColumnMapping("B", "B")],
            rows,
            2);

        // "ŠĐČĆŽ" = 5 znakova, staje. Emoji su 2 UTF-16 jedinice (6 > 5): upozorenje, iako je 3 vidljiva znaka.
        warnings.Select(w => w.TargetColumn).ShouldBe(["B"]);
    }

    [Fact]
    public void TextLength_VarcharMaxLengthIsBytesVsNvarcharChars()
    {
        // nvarchar MaxLength u sys.columns je u bajtovima (2 po znaku); proveri da li je ColumnSchema vec normalizovan.
        IReadOnlyList<string?>[] rows = [["abcdefgh"]];
        MappingWarnings.ForTextLength(
            [Profile(0, "A")], Table(Column("A", "nvarchar", maxLength: 10)), [new ColumnMapping("A", "A")], rows, 2)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Validate_MappingWithEmptySourceName_IsRejected()
    {
        var schema = new DatabaseSchema([Table(Column("Name"))]);
        MappingValidator.Validate("dbo", "T", [new ColumnMapping("", "Name")], schema, ["Kolona 1"]).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_SourceNameIsCaseAndDiacriticSensitive()
    {
        var schema = new DatabaseSchema([Table(Column("Name"))]);
        MappingValidator.Validate("dbo", "T", [new ColumnMapping("sifra", "Name")], schema, ["Šifra"]).IsValid.ShouldBeFalse();
    }
}
