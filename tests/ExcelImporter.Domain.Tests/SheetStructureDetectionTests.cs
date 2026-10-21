using System.Globalization;
using ExcelImporter.Domain.Columns;
using Shouldly;

namespace ExcelImporter.Domain.Tests;

public sealed class SheetStructureDetectionTests
{
    private static string?[][] Rows(params string?[][] rows) => rows;

    [Theory]
    [InlineData("42", ColumnType.WholeNumber)]
    [InlineData("-7", ColumnType.WholeNumber)]
    [InlineData("0", ColumnType.WholeNumber)]
    [InlineData("3.14", ColumnType.DecimalNumber)]
    [InlineData("0.5", ColumnType.DecimalNumber)]
    [InlineData("1E-05", ColumnType.DecimalNumber)]
    [InlineData("2024-03-15", ColumnType.Date)]
    [InlineData("2024-03-15T10:30:00", ColumnType.Date)]
    [InlineData("15.03.2024", ColumnType.Date)]
    [InlineData("true", ColumnType.Boolean)]
    [InlineData("FALSE", ColumnType.Boolean)]
    [InlineData("Beograd", ColumnType.Text)]
    public void Classify_RecognizesFiveTypes(string value, ColumnType expected) =>
        CellValueClassifier.Classify(value).ShouldBe(expected);

    [Theory]
    [InlineData("1,5")]
    [InlineData("1,000")]
    [InlineData("1.000,50")]
    [InlineData("007")]
    [InlineData("00.5")]
    [InlineData("1 000")]
    [InlineData("03/04/2024")]
    [InlineData("2024-13-45")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Classify_KeepsAmbiguousValuesAsText(string value) =>
        CellValueClassifier.Classify(value).ShouldBe(ColumnType.Text);

    [Fact]
    public void Classify_IntegerBeyondInt64_IsDecimal() =>
        CellValueClassifier.Classify("99999999999999999999").ShouldBe(ColumnType.DecimalNumber);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_ReturnsNull_ForEmptyCell(string? value) => CellValueClassifier.Classify(value).ShouldBeNull();

    [Fact]
    public void Classify_IsIndependentOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sr-Latn-RS");
            CellValueClassifier.Classify("1.5").ShouldBe(ColumnType.DecimalNumber);
            CellValueClassifier.Classify("1,5").ShouldBe(ColumnType.Text);
            CellValueClassifier.Classify("2024-03-15").ShouldBe(ColumnType.Date);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Detect_IntegersAndEmptyCells_AreInteger()
    {
        var result = ColumnTypeDetector.Detect(["1", null, "2", "", "3"]);

        result.ShouldBe((ColumnType.WholeNumber, false, false, 3));
    }

    [Fact]
    public void Detect_IntegersAndDecimals_AreDecimal() =>
        ColumnTypeDetector.Detect(["1", "2.5", "3"]).Type.ShouldBe(ColumnType.DecimalNumber);

    [Fact]
    public void Detect_DatesWithAndWithoutTime_AreDate() =>
        ColumnTypeDetector.Detect(["2024-01-01", "2024-01-02T08:00:00"]).Type.ShouldBe(ColumnType.Date);

    [Fact]
    public void Detect_NumbersAndText_AreMixedText()
    {
        var result = ColumnTypeDetector.Detect(["1", "abc", "3"]);

        result.Type.ShouldBe(ColumnType.Text);
        result.IsMixed.ShouldBeTrue();
    }

    [Fact]
    public void Detect_DateSerialNumbersAndDates_AreMixedText()
    {
        // Datum koji stigne kao serijski broj (45000) ne meša se tiho sa pravim datumima.
        var result = ColumnTypeDetector.Detect(["45000", "2024-01-01"]);

        result.Type.ShouldBe(ColumnType.Text);
        result.IsMixed.ShouldBeTrue();
    }

    [Fact]
    public void Detect_ColumnWithoutValues_IsEmptyText()
    {
        var result = ColumnTypeDetector.Detect([null, " ", ""]);

        result.ShouldBe((ColumnType.Text, true, false, 0));
    }

    [Fact]
    public void Detect_DecimalWithCommaAmongDecimalsWithDot_IsMixedText()
    {
        var result = ColumnTypeDetector.Detect(["1.5", "2,5"]);

        result.Type.ShouldBe(ColumnType.Text);
        result.IsMixed.ShouldBeTrue();
    }

    [Fact]
    public void Detect_BooleanColumn_IsBoolean() =>
        ColumnTypeDetector.Detect(["true", "false", null]).Type.ShouldBe(ColumnType.Boolean);

    [Fact]
    public void Structure_FindsHeaderInFirstRow()
    {
        var rows = Rows(["Id", "Naziv", "Cena"], ["1", "A", "1.5"], ["2", "B", "2.5"]);

        var structure = SheetStructureDetector.Detect(rows);

        structure.HeaderRowIndex.ShouldBe(0);
        structure.HeaderChosenManually.ShouldBeFalse();
        structure.Columns.Select(c => (c.Name, c.Type)).ShouldBe(
            [("Id", ColumnType.WholeNumber), ("Naziv", ColumnType.Text), ("Cena", ColumnType.DecimalNumber)]);
    }

    [Fact]
    public void Structure_FindsHeaderInSecondRow_AfterTitleRow()
    {
        var rows = Rows(["Izveštaj za 2024", null, null], ["Id", "Naziv", "Cena"], ["1", "A", "1.5"]);

        SheetStructureDetector.Detect(rows).HeaderRowIndex.ShouldBe(1);
    }

    [Fact]
    public void Structure_FindsHeaderInThirdRow_AfterEmptyRowAndTitle()
    {
        var rows = Rows([null, null, null], ["Firma d.o.o.", null, null], ["Id", "Naziv", "Cena"], ["1", "A", "1.5"]);

        SheetStructureDetector.Detect(rows).HeaderRowIndex.ShouldBe(2);
    }

    [Fact]
    public void Structure_HasNoHeader_WhenFirstRowIsData()
    {
        var rows = Rows(["1", "A", "1.5"], ["2", "B", "2.5"]);

        var structure = SheetStructureDetector.Detect(rows);

        structure.HeaderRowIndex.ShouldBeNull();
        structure.Columns.Select(c => c.Name).ShouldBe(["Kolona 1", "Kolona 2", "Kolona 3"]);
        structure.Columns[0].Type.ShouldBe(ColumnType.WholeNumber);
        structure.Columns[0].NonEmptyCount.ShouldBe(2);
    }

    [Fact]
    public void Structure_RejectsHeaderWithDuplicateNames()
    {
        var rows = Rows(["A", "A", "B"], ["Id", "Naziv", "Cena"], ["1", "x", "2"]);

        SheetStructureDetector.Detect(rows).HeaderRowIndex.ShouldBe(1);
    }

    [Fact]
    public void Structure_ManualHeaderRow_OverridesAutomaticAndChangesResult()
    {
        var rows = Rows(["Id", "Naziv"], ["Šifra", "Ime"], ["1", "A"]);

        var automatic = SheetStructureDetector.Detect(rows);
        var manual = SheetStructureDetector.Detect(rows, headerRowIndex: 1);

        automatic.HeaderRowIndex.ShouldBe(0);
        automatic.Columns[0].Type.ShouldBe(ColumnType.Text);
        manual.HeaderRowIndex.ShouldBe(1);
        manual.HeaderChosenManually.ShouldBeTrue();
        manual.Columns.Select(c => c.Name).ShouldBe(["Šifra", "Ime"]);
        manual.Columns[0].Type.ShouldBe(ColumnType.WholeNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Structure_ThrowsForManualRowOutsideSheet(int index)
    {
        var rows = Rows(["A"], ["1"], ["2"]);

        Should.Throw<ArgumentOutOfRangeException>(() => SheetStructureDetector.Detect(rows, index));
    }

    [Fact]
    public void Structure_UsesFallbackNameForEmptyHeaderCell_AndHandlesShortRows()
    {
        var rows = Rows(["Id", null, "Cena"], ["1", "x", "1.5"], ["2"]);

        var structure = SheetStructureDetector.Detect(rows);

        structure.Columns.Select(c => c.Name).ShouldBe(["Id", "Kolona 2", "Cena"]);
        structure.Columns[2].NonEmptyCount.ShouldBe(1);
    }

    [Fact]
    public void Structure_OnlyHeader_GivesEmptyColumns()
    {
        var structure = SheetStructureDetector.Detect(Rows(["Id", "Naziv"]));

        structure.Columns.ShouldAllBe(c => c.IsEmpty && c.Type == ColumnType.Text);
    }

    [Fact]
    public void Structure_EmptySheet_HasNoHeaderAndNoColumns()
    {
        var structure = SheetStructureDetector.Detect(Rows());

        structure.HeaderRowIndex.ShouldBeNull();
        structure.Columns.ShouldBeEmpty();
    }

    [Fact]
    public void Structure_TypesAreDecidedOnlyOverSampleRows()
    {
        List<IReadOnlyList<string?>> rows = [["Broj"]];
        rows.AddRange(Enumerable.Range(0, 1000).Select(i => (IReadOnlyList<string?>)[i.ToString(CultureInfo.InvariantCulture)]));
        rows.Add(["nije broj"]);

        SheetStructureDetector.Detect(rows).Columns[0].Type.ShouldBe(ColumnType.WholeNumber);
        SheetStructureDetector.Detect(rows, sampleRowCount: 2000).Columns[0].Type.ShouldBe(ColumnType.Text);
    }
}
