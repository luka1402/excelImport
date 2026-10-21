using ExcelImporter.Domain.Columns;
using Shouldly;

namespace ExcelImporter.Domain.Tests;

/// <summary>QA (COA-35): header van prvog reda, prazne celije, mesani tipovi.</summary>
public sealed class QaHeaderDetectionTests
{
    private static string?[][] Rows(params string?[][] rows) => rows;

    [Fact]
    public void Header_AfterTitleAndBlankRows_IsFound()
    {
        var rows = Rows(
            ["Izvestaj o prodaji", null, null],
            [null, null, null],
            ["Sifra", "Naziv", "Cena"],
            ["A1", "Olovka", "10.5"],
            ["A2", "Sveska", "20"]);

        var s = SheetStructureDetector.Detect(rows);

        s.HeaderRowIndex.ShouldBe(2);
        s.Columns.Select(c => c.Name).ShouldBe(["Sifra", "Naziv", "Cena"]);
        s.Columns[2].Type.ShouldBe(ColumnType.DecimalNumber);
    }

    [Fact]
    public void Header_BeyondSearchWindow_IsNotFound()
    {
        var list = new List<string?[]>();
        for (var i = 0; i < 10; i++)
        {
            list.Add(["1", "2", "3"]);
        }

        list.Add(["A", "B", "C"]);
        list.Add(["1", "2", "3"]);

        SheetStructureDetector.Detect(list.ToArray()).HeaderRowIndex.ShouldBeNull();
    }

    [Fact]
    public void Header_WithEmptyMiddleCell_GetsGeneratedName()
    {
        var s = SheetStructureDetector.Detect(Rows(["Ime", null, "Prezime"], ["Ana", "x", "Ilic"]));

        s.Columns.Select(c => c.Name).ShouldBe(["Ime", "Kolona 2", "Prezime"]);
    }

    [Fact]
    public void Header_WithWhitespaceOnlyCell_GetsGeneratedName()
    {
        var s = SheetStructureDetector.Detect(Rows(["A", "   ", "C"], ["1", "2", "3"]));

        s.Columns[1].Name.ShouldBe("Kolona 2");
    }

    [Fact]
    public void Header_NamesAreTrimmed_CyrillicAndEmojiPreserved()
    {
        var s = SheetStructureDetector.Detect(Rows(["  Име ", "Цена😀", "Šđčćž"], ["Ана", "5", "x"]));

        s.Columns.Select(c => c.Name).ShouldBe(["Име", "Цена😀", "Šđčćž"]);
    }

    [Fact]
    public void Header_WithDuplicateNames_IsNotAutoDetectedAsHeader()
    {
        SheetStructureDetector.Detect(Rows(["A", "a"], ["1", "2"])).HeaderRowIndex.ShouldBeNull();
    }

    [Fact]
    public void Header_WithNumericYearColumns_IsDetected()
    {
        var s = SheetStructureDetector.Detect(Rows(["Ime", "2023", "2024"], ["Ana", "10", "20"], ["Ivan", "11", "21"]));

        s.HeaderRowIndex.ShouldBe(0);
    }

    [Fact]
    public void Header_ShortTitleRowWithTwoOfThreeCells_IsNotTakenAsHeader()
    {
        var s = SheetStructureDetector.Detect(Rows(
            ["Izvestaj", "Beograd", null],
            ["Sifra", "Naziv", "Cena"],
            ["A1", "Olovka", "10"]));

        s.HeaderRowIndex.ShouldBe(1);
    }

    [Fact]
    public void AllEmptyRows_NoHeader_OnlyEmptyColumns()
    {
        var s = SheetStructureDetector.Detect(Rows([null, null], ["", " "]));

        s.HeaderRowIndex.ShouldBeNull();
        s.Columns.ShouldAllBe(c => c.IsEmpty && c.Type == ColumnType.Text);
    }

    [Fact]
    public void ZeroRows_DoesNotThrow()
    {
        var s = SheetStructureDetector.Detect(Rows());

        s.HeaderRowIndex.ShouldBeNull();
        s.Columns.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void ManualHeader_OutOfRange_Throws(int index) =>
        Should.Throw<ArgumentOutOfRangeException>(() => SheetStructureDetector.Detect(Rows(["A"], ["1"]), index));

    [Fact]
    public void ManualHeader_OverridesAuto_AndIsFlagged()
    {
        var s = SheetStructureDetector.Detect(Rows(["A", "B"], ["x", "y"], ["1", "2"]), headerRowIndex: 1);

        s.HeaderChosenManually.ShouldBeTrue();
        s.HeaderRowIndex.ShouldBe(1);
        s.Columns.Select(c => c.Name).ShouldBe(["x", "y"]);
        s.Columns[0].Type.ShouldBe(ColumnType.WholeNumber);
    }

    [Fact]
    public void ManualHeader_OnLastRow_YieldsEmptyColumns()
    {
        var s = SheetStructureDetector.Detect(Rows(["A", "B"], ["x", "y"]), headerRowIndex: 1);

        s.Columns.ShouldAllBe(c => c.IsEmpty);
    }

    [Fact]
    public void MixedColumn_NumbersAndText_IsTextMixed()
    {
        var s = SheetStructureDetector.Detect(Rows(["K"], ["1"], ["abc"], ["2"]));

        s.Columns[0].Type.ShouldBe(ColumnType.Text);
        s.Columns[0].IsMixed.ShouldBeTrue();
    }

    [Fact]
    public void MixedColumn_BoolAndNumber_DateAndNumber_AreMixed()
    {
        ColumnTypeDetector.Detect(["true", "1"]).IsMixed.ShouldBeTrue();
        ColumnTypeDetector.Detect(["2024-01-01", "5"]).IsMixed.ShouldBeTrue();
    }

    [Fact]
    public void Column_WithEmptyCellsOnly_IsEmptyNotMixed()
    {
        var s = SheetStructureDetector.Detect(Rows(["A", "B"], ["1", null], ["2", ""]));

        s.Columns[1].IsEmpty.ShouldBeTrue();
        s.Columns[1].IsMixed.ShouldBeFalse();
        s.Columns[1].NonEmptyCount.ShouldBe(0);
    }

    [Fact]
    public void RaggedRows_ShorterRowsTreatedAsEmptyCells()
    {
        var s = SheetStructureDetector.Detect(Rows(["A", "B", "C"], ["1"], ["2", "x"], ["3", "y", "4"]));

        s.Columns.Count.ShouldBe(3);
        s.Columns[2].Type.ShouldBe(ColumnType.WholeNumber);
        s.Columns[2].NonEmptyCount.ShouldBe(1);
    }

    [Fact]
    public void LeadingZeroCodes_StayText()
    {
        SheetStructureDetector.Detect(Rows(["Sifra"], ["007"], ["012"])).Columns[0].Type.ShouldBe(ColumnType.Text);
    }

    [Fact]
    public void SampleLimit_ValuesBeyondSample_AreIgnored()
    {
        List<string?[]> list = [["N"]];
        for (var i = 0; i < 1000; i++)
        {
            list.Add([i.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        list.Add(["tekst posle uzorka"]);

        SheetStructureDetector.Detect(list.ToArray()).Columns[0].Type.ShouldBe(ColumnType.WholeNumber);
    }

    [Fact]
    public void AllTextTable_FirstRowBecomesHeader() =>
        SheetStructureDetector.Detect(Rows(["Ana", "Beograd"], ["Ivan", "Nis"])).HeaderRowIndex.ShouldBe(0);

    [Fact]
    public void NoHeader_NumericOnlyData_NamesAreGenerated()
    {
        var s = SheetStructureDetector.Detect(Rows(["1", "2"], ["3", "4"]));

        s.HeaderRowIndex.ShouldBeNull();
        s.Columns.Select(c => c.Name).ShouldBe(["Kolona 1", "Kolona 2"]);
        s.Columns[0].NonEmptyCount.ShouldBe(2);
    }
}
