using System.Globalization;
using ExcelImporter.Domain.Rules;
using Shouldly;

namespace ExcelImporter.Domain.Tests;

public sealed class CellRuleEvaluatorTests
{
    private static Dictionary<string, object?> Row(object? value, string column = "A") => new() { [column] = value };

    private static CellRuleResult Transform(TransformKind kind, object? cell, string? format = null, string? separator = null) =>
        CellRuleEvaluator.Evaluate(
            new TransformRule { TargetField = "A", Kind = kind, Format = format, Separator = separator }, Row(cell), 7);

    [Theory]
    [InlineData(TransformKind.Trim, "  abc  ", "abc")]
    [InlineData(TransformKind.Upper, "abC", "ABC")]
    [InlineData(TransformKind.Lower, "AbC", "abc")]
    public void Text_transforms_are_applied(TransformKind kind, string input, string expected)
    {
        var result = Transform(kind, input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TransformKind.Trim)]
    [InlineData(TransformKind.Upper)]
    [InlineData(TransformKind.DateFormat)]
    [InlineData(TransformKind.DecimalSeparator)]
    public void Empty_cell_is_left_empty(TransformKind kind)
    {
        var result = Transform(kind, null, "dd.MM.yyyy", ",");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void DateFormat_parses_text_date()
    {
        var result = Transform(TransformKind.DateFormat, " 31.12.2025 ", format: "dd.MM.yyyy");

        result.Value.ShouldBe(new DateTime(2025, 12, 31));
    }

    [Fact]
    public void DateFormat_keeps_native_excel_date()
    {
        var date = new DateTime(2025, 1, 2);

        Transform(TransformKind.DateFormat, date, format: "dd.MM.yyyy").Value.ShouldBe(date);
    }

    [Theory]
    [InlineData("31.13.2025")]
    [InlineData("2025-12-31")]
    [InlineData("abc")]
    public void DateFormat_invalid_value_reports_row_column_and_reason(string input)
    {
        var result = Transform(TransformKind.DateFormat, input, format: "dd.MM.yyyy");

        result.IsSuccess.ShouldBeFalse();
        result.Failure!.RowNumber.ShouldBe(7);
        result.Failure.ColumnName.ShouldBe("A");
        result.Failure.Reason.ShouldContain(input);
        result.Failure.Reason.ShouldContain("dd.MM.yyyy");
        result.Value.ShouldBe(input);
    }

    [Fact]
    public void DateFormat_without_format_fails()
    {
        Transform(TransformKind.DateFormat, "01.01.2025").IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("1234,56", ",", "1234.56")]
    [InlineData("1.234,56", ",", "1234.56")]
    [InlineData("-0,5", ",", "-0.5")]
    [InlineData("1,234.56", ".", "1234.56")]
    [InlineData("12.5", ".", "12.5")]
    public void DecimalSeparator_parses_text_number(string input, string separator, string expected)
    {
        var result = Transform(TransformKind.DecimalSeparator, input, separator: separator);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DecimalSeparator_keeps_native_number()
    {
        Transform(TransformKind.DecimalSeparator, 12.5, separator: ",").Value.ShouldBe(12.5);
    }

    [Theory]
    [InlineData("abc", ",")]
    [InlineData("1,2,3x", ",")]
    [InlineData("1.5", "x")]
    public void DecimalSeparator_invalid_value_reports_reason(string input, string separator)
    {
        var result = Transform(TransformKind.DecimalSeparator, input, separator: separator);

        result.IsSuccess.ShouldBeFalse();
        result.Failure!.RowNumber.ShouldBe(7);
        result.Failure.ColumnName.ShouldBe("A");
        result.Failure.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null, "N/A")]
    [InlineData("", "N/A")]
    [InlineData("   ", "N/A")]
    [InlineData("x", "x")]
    [InlineData(0, 0)]
    public void Default_applies_only_to_empty_cell(object? input, object expected)
    {
        var rule = new DefaultValueRule { TargetField = "A", Value = "N/A" };

        CellRuleEvaluator.Evaluate(rule, Row(input), 1).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Da", "1")]
    [InlineData("da", "1")]
    [InlineData("Ne", "0")]
    [InlineData("Možda", "0")]
    public void Conditional_maps_Da_to_1_and_Ne_to_0(string input, string expected)
    {
        var rule = new ConditionalValueRule
        {
            TargetField = "Active",
            Conditions = [new Condition("Active", ComparisonOperator.Eq, "Da")],
            Value = "1",
            ElseValue = "0",
        };

        CellRuleEvaluator.Evaluate(rule, Row(input, "Active"), 1).Value.ShouldBe(expected);
    }

    [Fact]
    public void Conditional_requires_all_conditions()
    {
        var rule = new ConditionalValueRule
        {
            TargetField = "Tier",
            Conditions = [new Condition("Amount", ComparisonOperator.Gt, "100"), new Condition("Country", ComparisonOperator.Eq, "RS")],
            Value = "VIP",
            ElseValue = "Standard",
        };

        var both = new Dictionary<string, object?> { ["Amount"] = 150m, ["Country"] = "RS" };
        var one = new Dictionary<string, object?> { ["Amount"] = 150m, ["Country"] = "DE" };

        CellRuleEvaluator.Evaluate(rule, both, 1).Value.ShouldBe("VIP");
        CellRuleEvaluator.Evaluate(rule, one, 1).Value.ShouldBe("Standard");
    }

    [Fact]
    public void Conditional_without_else_keeps_original_value()
    {
        var rule = new ConditionalValueRule
        {
            TargetField = "A",
            Conditions = [new Condition("A", ComparisonOperator.Eq, "x")],
            Value = "y",
        };

        CellRuleEvaluator.Evaluate(rule, Row("z"), 1).Value.ShouldBe("z");
    }

    [Fact]
    public void Unsupported_rule_type_throws()
    {
        var rule = new RowFilterRule { Action = RowFilterAction.Include, Conditions = [] };

        Should.Throw<ArgumentException>(() => CellRuleEvaluator.Evaluate(rule, Row(null), 1));
    }
}
