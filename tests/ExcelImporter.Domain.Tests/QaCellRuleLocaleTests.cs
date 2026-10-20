using ExcelImporter.Domain.Rules;
using Shouldly;

namespace ExcelImporter.Domain.Tests;

/// <summary>QA (COA-53): lokalizovani datumi/decimale, prazno vs null, ćirilica.</summary>
public sealed class QaCellRuleLocaleTests
{
    private static Dictionary<string, object?> Row(object? v) => new() { ["A"] = v };

    private static CellRuleResult T(TransformKind k, object? v, string? f = null, string? s = null) =>
        CellRuleEvaluator.Evaluate(new TransformRule { TargetField = "A", Kind = k, Format = f, Separator = s }, Row(v), 3);

    [Theory]
    [InlineData("1.234,56", ",", 1234.56)]
    [InlineData("1234,56", ",", 1234.56)]
    [InlineData("-0,5", ",", -0.5)]
    [InlineData("1,234.56", ".", 1234.56)]
    [InlineData("12.5", ".", 12.5)]
    public void Decimal_valid_inputs(string input, string sep, double expected) =>
        T(TransformKind.DecimalSeparator, input, s: sep).Value.ShouldBe((decimal)expected);

    // Dvosmislen ulaz: "1.5" sa separatorom "," ne sme tiho postati 15.
    [Theory]
    [InlineData("1.5", ",")]
    [InlineData("1,5", ".")]
    [InlineData("12.34", ",")]
    public void Decimal_wrong_separator_must_not_silently_change_magnitude(string input, string sep)
    {
        var r = T(TransformKind.DecimalSeparator, input, s: sep);
        if (r.IsSuccess)
        {
            r.Value.ShouldNotBe(decimal.Parse(input.Replace(".", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("1e5")]
    [InlineData("12,5 EUR")]
    public void Decimal_invalid_fails_with_row_and_column(string input)
    {
        var r = T(TransformKind.DecimalSeparator, input, s: ",");
        r.IsSuccess.ShouldBeFalse();
        r.Failure!.RowNumber.ShouldBe(3);
        r.Failure.ColumnName.ShouldBe("A");
        r.Value.ShouldBe(input);
    }

    [Theory]
    [InlineData("31.12.2024", "dd.MM.yyyy")]
    [InlineData(" 01.02.2024 ", "dd.MM.yyyy")]
    [InlineData("2024-02-29", "yyyy-MM-dd")]
    public void Date_valid(string input, string fmt) => T(TransformKind.DateFormat, input, fmt).Value.ShouldBeOfType<DateTime>();

    [Theory]
    [InlineData("29.02.2023", "dd.MM.yyyy")]
    [InlineData("32.01.2024", "dd.MM.yyyy")]
    [InlineData("12/31/2024", "dd.MM.yyyy")]
    [InlineData("1.2.2024", "dd.MM.yyyy")]
    [InlineData("31.12.2024 10:00", "dd.MM.yyyy")]
    public void Date_invalid_fails(string input, string fmt) =>
        T(TransformKind.DateFormat, input, fmt).IsSuccess.ShouldBeFalse();

    [Fact]
    public void Date_serial_number_from_excel_is_not_crashing() =>
        Should.NotThrow(() => T(TransformKind.DateFormat, 45000d, "dd.MM.yyyy"));

    [Fact]
    public void Date_month_names_are_culture_invariant()
    {
        // Srpski nazivi meseca ne smeju zavisiti od kulture servera.
        T(TransformKind.DateFormat, "15 januar 2024", "dd MMMM yyyy").IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TransformKind.Trim)]
    [InlineData(TransformKind.Upper)]
    [InlineData(TransformKind.DateFormat)]
    [InlineData(TransformKind.DecimalSeparator)]
    public void Whitespace_only_is_treated_like_empty_not_failed(TransformKind k)
    {
        var r = T(k, "   ", "dd.MM.yyyy", ",");
        r.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Default_applies_to_null_empty_and_whitespace_but_not_zero_or_false()
    {
        var rule = new DefaultValueRule { TargetField = "A", Value = "X" };
        CellRuleEvaluator.Evaluate(rule, Row(null), 1).Value.ShouldBe("X");
        CellRuleEvaluator.Evaluate(rule, Row(""), 1).Value.ShouldBe("X");
        CellRuleEvaluator.Evaluate(rule, Row("   "), 1).Value.ShouldBe("X");
        CellRuleEvaluator.Evaluate(rule, new Dictionary<string, object?>(), 1).Value.ShouldBe("X");
        CellRuleEvaluator.Evaluate(rule, Row(0), 1).Value.ShouldBe(0);
        CellRuleEvaluator.Evaluate(rule, Row(false), 1).Value.ShouldBe(false);
        CellRuleEvaluator.Evaluate(rule, Row("0"), 1).Value.ShouldBe("0");
    }

    [Fact]
    public void Conditional_else_empty_string_differs_from_null()
    {
        var cond = new[] { new Condition("A", ComparisonOperator.Eq, "Da") };
        var withEmptyElse = new ConditionalValueRule { TargetField = "B", Conditions = cond, Value = "1", ElseValue = "" };
        var noElse = new ConditionalValueRule { TargetField = "B", Conditions = cond, Value = "1" };
        var row = new Dictionary<string, object?> { ["A"] = "Ne", ["B"] = "orig" };
        CellRuleEvaluator.Evaluate(withEmptyElse, row, 1).Value.ShouldBe("");
        CellRuleEvaluator.Evaluate(noElse, row, 1).Value.ShouldBe("orig");
    }

    [Fact]
    public void Conditional_cyrillic_da_ne_case_insensitive()
    {
        var cond = new[] { new Condition("A", ComparisonOperator.Eq, "Да") };
        var rule = new ConditionalValueRule { TargetField = "B", Conditions = cond, Value = "1", ElseValue = "0" };
        CellRuleEvaluator.Evaluate(rule, new Dictionary<string, object?> { ["A"] = "да", ["B"] = null }, 1).Value.ShouldBe("1");
        CellRuleEvaluator.Evaluate(rule, new Dictionary<string, object?> { ["A"] = "НЕ", ["B"] = null }, 1).Value.ShouldBe("0");
    }

    [Theory]
    [InlineData("  љубав ", "ЉУБАВ", "љубав")]
    [InlineData("Šđčćž ЂЋЏ", "ŠĐČĆŽ ЂЋЏ", "šđčćž ђћџ")]
    public void Cyrillic_and_serbian_latin_case(string input, string up, string low)
    {
        T(TransformKind.Upper, input.Trim()).Value.ShouldBe(up);
        T(TransformKind.Lower, input.Trim()).Value.ShouldBe(low);
    }

    [Fact]
    public void Trim_removes_nbsp_and_tabs()
    {
        T(TransformKind.Trim, " abc\t\n").Value.ShouldBe("abc");
    }

    [Fact]
    public void Numeric_cell_text_transform_uses_invariant_culture()
    {
        T(TransformKind.Upper, 1234.5m).Value.ShouldBe("1234.5");
    }

    [Fact]
    public void Decimal_double_cell_passthrough_and_bool_fails()
    {
        T(TransformKind.DecimalSeparator, 12.5d, s: ",").Value.ShouldBe(12.5d);
        T(TransformKind.DecimalSeparator, true, s: ",").IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Decimal_huge_value_does_not_throw() =>
        Should.NotThrow(() => T(TransformKind.DecimalSeparator, new string('9', 40) + ",5", s: ","));
}


