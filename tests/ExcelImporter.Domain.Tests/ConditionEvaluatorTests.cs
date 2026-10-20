using ExcelImporter.Domain.Rules;
using Shouldly;
using static ExcelImporter.Domain.Rules.ComparisonOperator;

namespace ExcelImporter.Domain.Tests;

public class ConditionEvaluatorTests
{
    [Theory]
    // brojevi (numeričko poređenje, ne tekstualno: "10" > "9")
    [InlineData(Eq, 5, "5", true)]
    [InlineData(Eq, 5.0, "5", true)]
    [InlineData(Eq, "5.00", "5", true)]
    [InlineData(Neq, 5, "6", true)]
    [InlineData(Neq, 5, "5", false)]
    [InlineData(Gt, 10, "9", true)]
    [InlineData(Gt, "10", "9", true)]
    [InlineData(Gt, 9, "10", false)]
    [InlineData(Lt, 9, "10", true)]
    [InlineData(Lt, 10, "10", false)]
    [InlineData(Gte, 10, "10", true)]
    [InlineData(Gte, 9, "10", false)]
    [InlineData(Lte, 10, "10", true)]
    [InlineData(Lte, 11, "10", false)]
    [InlineData(Gt, -1.5, "-2", true)]
    // tekst (bez razlike u veličini slova)
    [InlineData(Eq, "Abc", "abc", true)]
    [InlineData(Eq, "Abc", "abd", false)]
    [InlineData(Neq, "Abc", "abd", true)]
    [InlineData(Gt, "b", "a", true)]
    [InlineData(Lt, "a", "b", true)]
    [InlineData(Gte, "a", "a", true)]
    [InlineData(Lte, "b", "a", false)]
    [InlineData(Contains, "Hello World", "lo wo", true)]
    [InlineData(Contains, "Hello", "xyz", false)]
    [InlineData(Contains, 12345, "234", true)]
    [InlineData(StartsWith, "Hello", "he", true)]
    [InlineData(StartsWith, "Hello", "lo", false)]
    // prazno
    [InlineData(IsEmpty, null, null, true)]
    [InlineData(IsEmpty, "", null, true)]
    [InlineData(IsEmpty, "   ", null, true)]
    [InlineData(IsEmpty, "x", null, false)]
    [InlineData(IsEmpty, 0, null, false)]
    [InlineData(IsNotEmpty, "x", null, true)]
    [InlineData(IsNotEmpty, 0, null, true)]
    [InlineData(IsNotEmpty, null, null, false)]
    [InlineData(IsNotEmpty, " ", null, false)]
    [InlineData(Eq, null, "", true)]
    [InlineData(Eq, null, "x", false)]
    [InlineData(Neq, null, "x", true)]
    [InlineData(Lt, null, "x", true)]
    [InlineData(Gt, null, "x", false)]
    [InlineData(Contains, null, "x", false)]
    [InlineData(StartsWith, null, "x", false)]
    public void Evaluate_ShouldCompare_NumbersTextAndEmpty(ComparisonOperator op, object? cell, string? expected, bool result) =>
        ConditionEvaluator.Evaluate(op, cell, expected).ShouldBe(result);

    [Theory]
    [InlineData(Eq, "2024-03-15", "2024-03-15", true)]
    [InlineData(Eq, "2024-03-15T00:00:00", "2024-03-15", true)]
    [InlineData(Gt, "2024-12-01", "2024-03-15", true)]
    [InlineData(Gt, "2024-03-15", "2024-12-01", false)]
    [InlineData(Lt, "2024-03-15", "2024-12-01", true)]
    [InlineData(Gte, "2024-03-15", "2024-03-15", true)]
    [InlineData(Lte, "2024-03-16", "2024-03-15", false)]
    [InlineData(Neq, "2024-03-16", "2024-03-15", true)]
    public void Evaluate_ShouldCompareDates_FromText(ComparisonOperator op, string cell, string expected, bool result) =>
        ConditionEvaluator.Evaluate(op, cell, expected).ShouldBe(result);

    [Fact]
    public void Evaluate_ShouldCompareDates_FromTypedCells()
    {
        ConditionEvaluator.Evaluate(Gt, new DateTime(2024, 12, 1), "2024-03-15").ShouldBeTrue();
        ConditionEvaluator.Evaluate(Lt, new DateOnly(2024, 1, 1), "2024-03-15").ShouldBeTrue();
        ConditionEvaluator.Evaluate(Eq, new DateOnly(2024, 3, 15), "2024-03-15").ShouldBeTrue();
        ConditionEvaluator.Evaluate(Eq, new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero), "2024-03-15").ShouldBeTrue();
    }

    [Fact]
    public void Evaluate_ShouldTreatMissingColumnAsEmpty()
    {
        var row = new Dictionary<string, object?> { ["A"] = 1 };

        ConditionEvaluator.Evaluate(new Condition("Missing", IsEmpty), row).ShouldBeTrue();
        ConditionEvaluator.Evaluate(new Condition("A", Eq, "1"), row).ShouldBeTrue();
    }

    [Fact]
    public void EvaluateAll_ShouldRequireEveryCondition()
    {
        var row = new Dictionary<string, object?> { ["A"] = 5, ["B"] = "x" };

        ConditionEvaluator.EvaluateAll([new Condition("A", Gt, "1"), new Condition("B", Eq, "x")], row).ShouldBeTrue();
        ConditionEvaluator.EvaluateAll([new Condition("A", Gt, "1"), new Condition("B", Eq, "y")], row).ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_ShouldThrow_ForUnknownOperator() =>
        Should.Throw<ArgumentOutOfRangeException>(() => ConditionEvaluator.Evaluate((ComparisonOperator)99, "a", "a"));

    [Fact]
    public void AllTenOperatorsAreCovered() => Enum.GetValues<ComparisonOperator>().Length.ShouldBe(10);
}