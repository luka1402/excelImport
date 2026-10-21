using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Schema;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class QaColumnConstantsEdgeTests
{
    private static ColumnSchema Column(string name, string dataType = "nvarchar", bool nullable = true, int? maxLength = 20, byte precision = 0, byte scale = 0) =>
        new(name, 1, dataType, nullable, maxLength, precision, scale, false, false);

    private static readonly TableSchema _table = new("dbo", "T",
    [
        Column("Txt"),
        Column("TxtMax", maxLength: -1),
        Column("TxtNotNull", nullable: false),
        Column("Dec", "decimal", true, null, 5, 2),
        Column("Small", "tinyint", true, null),
        Column("Flag", "bit", true, null),
        Column("G", "uniqueidentifier", true, null),
        Column("Bin", "varbinary", true, 10),
        Column("D", "date", true, null),
    ], [], []);

    private static IReadOnlyList<MappingError> V(string col, string literal) =>
        ColumnConstants.Validate(_table, [new ColumnConstant(col, ConstantValue.FromLiteral(literal))], []);

    [Theory]
    [InlineData("'; DROP TABLE Users; --")]
    [InlineData("1; DELETE FROM T")]
    [InlineData("<script>alert(1)</script>")]
    public void SqlText_AsConstant_IsKeptVerbatimAsData(string text)
    {
        V("TxtMax", text).ShouldBeEmpty();
        ColumnConstants.Resolve(_table, [new("TxtMax", ConstantValue.FromLiteral(text))], DateTime.Now)["TxtMax"].ShouldBe(text);
    }

    [Fact]
    public void SqlText_IntoNumericColumn_IsRejected() => V("Small", "1; DROP TABLE T").ShouldHaveSingleItem();

    [Fact]
    public void Length_ExactlyMaxOk_OneOverRejected()
    {
        V("Txt", new string('a', 20)).ShouldBeEmpty();
        V("Txt", new string('a', 21)).ShouldHaveSingleItem();
        V("TxtMax", new string('a', 100000)).ShouldBeEmpty();
    }

    [Fact]
    public void Cyrillic_AndEmoji_Accepted() => V("Txt", "Активан ✓ 😀").ShouldBeEmpty();

    [Theory]
    [InlineData("Small", "256")]
    [InlineData("Small", "-1")]
    [InlineData("Small", "1.5")]
    [InlineData("Dec", "1000.00")]
    [InlineData("Dec", "1.234")]
    [InlineData("Dec", "1,5")]
    [InlineData("Flag", "maybe")]
    [InlineData("G", "not-a-guid")]
    [InlineData("D", "31.02.2026x")]
    [InlineData("TxtNotNull", "   ")]
    public void WrongTypeOrRange_Rejected(string col, string literal) => V(col, literal).ShouldHaveSingleItem();

    [Theory]
    [InlineData("Dec", "999.99")]
    [InlineData("Small", "255")]
    [InlineData("Flag", "da")]
    [InlineData("G", "d3b07384-d9a0-4c3e-a4f7-3f1c1e2b4a11")]
    [InlineData("D", "2026-02-28")]
    public void Valid_Accepted(string col, string literal) => V(col, literal).ShouldBeEmpty();

    [Fact]
    public void UnsupportedType_Rejected() => V("Bin", "abc").ShouldHaveSingleItem();

    [Fact]
    public void DynamicOnTextColumn_Rejected() =>
        ColumnConstants.Validate(_table, [new("Txt", ConstantValue.FromDynamic(DynamicConstantKind.Now))], []).ShouldHaveSingleItem();

    [Fact]
    public void UndefinedEnum_Rejected() =>
        ColumnConstants.Validate(_table, [new("D", new ConstantValue { Dynamic = (DynamicConstantKind)99 })], []).ShouldHaveSingleItem();

    [Fact]
    public void NullConstantValue_ThrowsOrRejects()
    {
        var errors = ColumnConstants.Validate(_table, [new("D", null!)], []);

        errors.ShouldHaveSingleItem().TargetColumn.ShouldBe("D");
    }

    [Fact]
    public void WhitespaceLiteral_OnNullableText_BecomesNullSilently()
    {
        var r = ColumnConstants.Resolve(_table, [new("Txt", ConstantValue.FromLiteral("   "))], DateTime.Now);
        r["Txt"].ShouldBeNull();
    }
}
