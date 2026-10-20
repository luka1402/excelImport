using ExcelImporter.Domain.Rules;
using Shouldly;

namespace ExcelImporter.Domain.Tests;

public class RuleSerializerTests
{
    public static TheoryData<Rule> AllRuleKinds => new()
    {
        new ConditionalValueRule
        {
            TargetField = "Status",
            Conditions = [new Condition("Amount", ComparisonOperator.Gte, "100"), new Condition("Note", ComparisonOperator.IsEmpty)],
            Value = "Big",
            ElseValue = "Small",
        },
        new DefaultValueRule { TargetField = "Country", Value = "RS" },
        new TransformRule { TargetField = "Name", Kind = TransformKind.Upper },
        new LookupRule
        {
            TargetField = "Code",
            SourceField = "Name",
            Mappings = new Dictionary<string, string> { ["Beograd"] = "BG", ["Novi Sad"] = "NS" },
            NotFoundValue = "??",
        },
        new RowFilterRule { Action = RowFilterAction.Exclude, Conditions = [new Condition("Name", ComparisonOperator.StartsWith, "TEST")] },
    };

    [Theory]
    [MemberData(nameof(AllRuleKinds))]
    public void RoundTrip_ShouldPreserveRule(Rule rule)
    {
        var json = RuleSerializer.Serialize(rule);
        var restored = RuleSerializer.Deserialize(json);

        restored.GetType().ShouldBe(rule.GetType());
        RuleSerializer.Serialize(restored).ShouldBe(json);
        restored.Version.ShouldBe(rule.Version);
    }

    [Fact]
    public void Serialize_ShouldWriteTypeVersionAndOperatorNames()
    {
        var json = RuleSerializer.Serialize(new RowFilterRule
        {
            Action = RowFilterAction.Include,
            Conditions = [new Condition("A", ComparisonOperator.StartsWith, "x")],
        });

        json.ShouldBe("""{"version":1,"type":"rowFilter","action":"include","conditions":[{"field":"A","operator":"startsWith","value":"x"}]}""");
    }

    [Fact]
    public void RoundTrip_ShouldPreserveRuleList()
    {
        Rule[] rules = [new DefaultValueRule { TargetField = "A", Value = "1" }, new TransformRule { TargetField = "B", Kind = TransformKind.Lower }];

        var restored = RuleSerializer.DeserializeMany(RuleSerializer.Serialize(rules));

        restored.Count.ShouldBe(2);
        restored[0].ShouldBeOfType<DefaultValueRule>();
        restored[1].ShouldBeOfType<TransformRule>();
    }

    [Theory]
    [InlineData("""{"type":"nope","version":1}""", "Nepoznat tip pravila \"nope\"")]
    [InlineData("""{"type":"defaultValue","version":99,"targetField":"A","value":"1"}""", "Verzija 99")]
    [InlineData("""{"type":"defaultValue","targetField":"A","value":"1"}""", "\"version\"")]
    [InlineData("""{"version":1}""", "\"type\"")]
    [InlineData("""{"type":5,"version":1}""", "\"type\"")]
    [InlineData("""{"type":"defaultValue","version":1,"value":"1"}""", "defaultValue")]
    [InlineData("""{"type":"transform","version":1,"targetField":"A","kind":"bogus"}""", "transform")]
    [InlineData("""{"type":"defaultValue","version":1,"targetField":"A","value":"1","extra":1}""", "defaultValue")]
    [InlineData("[]", "JSON objekat")]
    [InlineData("{ not json", "JSON")]
    public void Deserialize_ShouldThrowClearError(string json, string expectedMessagePart)
    {
        var exception = Should.Throw<RuleSerializationException>(() => RuleSerializer.Deserialize(json));

        exception.Message.ShouldContain(expectedMessagePart);
    }

    [Fact]
    public void DeserializeMany_ShouldThrow_WhenNotAnArray() =>
        Should.Throw<RuleSerializationException>(() => RuleSerializer.DeserializeMany("{}"));

    [Fact]
    public void DeserializeMany_ShouldThrow_WhenAnyRuleIsUnknown() =>
        Should.Throw<RuleSerializationException>(() =>
            RuleSerializer.DeserializeMany("""[{"type":"defaultValue","version":1,"targetField":"A","value":"1"},{"type":"x","version":1}]"""));
}