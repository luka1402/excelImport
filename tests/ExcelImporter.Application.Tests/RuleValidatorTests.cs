using ExcelImporter.Application.Rules;
using ExcelImporter.Domain.Rules;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class RuleValidatorTests
{
    private static readonly RuleValidator Validator = new();
    private static readonly Condition ValidCondition = new("Status", ComparisonOperator.Eq, "A");

    public static TheoryData<Rule> ValidRules => new()
    {
        new ConditionalValueRule { TargetField = "T", Conditions = [ValidCondition], Value = "x", ElseValue = "y" },
        new DefaultValueRule { TargetField = "T", Value = "x" },
        new TransformRule { TargetField = "T", Kind = TransformKind.Trim },
        new LookupRule { TargetField = "T", SourceField = "S", Mappings = new Dictionary<string, string> { ["a"] = "b" } },
        new RowFilterRule { Action = RowFilterAction.Exclude, Conditions = [new Condition("Status", ComparisonOperator.IsEmpty)] },
    };

    public static TheoryData<Rule> InvalidRules => new()
    {
        new ConditionalValueRule { TargetField = "", Conditions = [ValidCondition], Value = "x" },
        new ConditionalValueRule { TargetField = "T", Conditions = [], Value = "x" },
        new ConditionalValueRule { TargetField = "T", Conditions = [new Condition("", ComparisonOperator.Eq, "A")], Value = "x" },
        new ConditionalValueRule { TargetField = "T", Conditions = [new Condition("S", ComparisonOperator.Gt)], Value = "x" },
        new ConditionalValueRule { TargetField = "T", Conditions = [new Condition("S", ComparisonOperator.IsEmpty, "x")], Value = "x" },
        new ConditionalValueRule { TargetField = "T", Conditions = [new Condition("S", (ComparisonOperator)99, "x")], Value = "x" },
        new DefaultValueRule { TargetField = "T", Value = " " },
        new TransformRule { TargetField = "T", Kind = (TransformKind)99 },
        new LookupRule { TargetField = "T", SourceField = "S", Mappings = new Dictionary<string, string>() },
        new LookupRule { TargetField = "T", SourceField = "", Mappings = new Dictionary<string, string> { ["a"] = "b" } },
        new RowFilterRule { Action = RowFilterAction.Include, Conditions = [] },
        new DefaultValueRule { TargetField = "T", Value = "x", Version = 2 },
    };

    [Theory]
    [MemberData(nameof(ValidRules))]
    public void Validate_ShouldSucceed_ForValidRule(Rule rule) =>
        Validator.Validate(rule).IsValid.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(InvalidRules))]
    public void Validate_ShouldFail_ForInvalidRule(Rule rule) =>
        Validator.Validate(rule).IsValid.ShouldBeFalse();
}