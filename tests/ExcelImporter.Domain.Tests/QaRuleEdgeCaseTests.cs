using ExcelImporter.Domain.Rules;
using Shouldly;
using static ExcelImporter.Domain.Rules.ComparisonOperator;

namespace ExcelImporter.Domain.Tests;

public class QaRuleEdgeCaseTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include"}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":[{"field":"A","operator":"bogus"}]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":[{"field":"A","operator":"eq","extra":1}]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":"x"}""")]
    [InlineData("""{"type":"rowFilter","version":1.5,"action":"include","conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":"1","action":"include","conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":0,"action":"include","conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":-1,"action":"include","conditions":[]}""")]
    [InlineData("""{"type":"defaultValue","version":1,"targetField":"A","value":5}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":[{"field":"A","operator":"eq","value":{"a":1}}]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"INCLUDE","conditions":[]}""")]
    [InlineData("""{"type":"ROWFILTER","version":1,"action":"include","conditions":[]}""")]
    [InlineData("{\"type\":\"rowFilter\",\"version\":1,\"action\":\"include\",\"conditions\":[]} trailing")]
    [InlineData("""{"type":"rowFilter","version":99999999999,"action":"include","conditions":[]}""")]
    public void Deserialize_BadInput_ShouldOnlyThrowRuleSerializationException(string json)
    {
        try
        {
            RuleSerializer.Deserialize(json);
        }
        catch (RuleSerializationException)
        {
            return;
        }

        Assert.Fail($"Očekivan RuleSerializationException za: {json}");
    }

    [Theory]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":null}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":[null]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":[{"operator":"eq","value":"1"}]}""")]
    [InlineData("""{"type":"lookup","version":1,"targetField":"A","sourceField":"B","table":null,"keyColumn":"K","returnColumn":"R"}""")]
    [InlineData("""{"type":"defaultValue","version":1,"targetField":"A","value":null}""")]
    [InlineData("""{"type":"defaultValue","version":1,"targetField":null,"value":"x"}""")]
    public void Deserialize_KnownDefects_ShouldThrowRuleSerializationException(string json) =>
        Should.Throw<RuleSerializationException>(() => RuleSerializer.Deserialize(json));

    [Theory]
    [InlineData("""{"type":"rowFilter","type":"x","version":1,"action":"include","conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"version":2,"action":"include","conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","action":"exclude","conditions":[]}""")]
    public void Deserialize_DuplicateKey_ShouldThrowRuleSerializationException(string json) =>
        Should.Throw<RuleSerializationException>(() => RuleSerializer.Deserialize(json));

    [Fact]
    public void DeserializeMany_DuplicateKey_ShouldThrowRuleSerializationException() =>
        Should.Throw<RuleSerializationException>(() => RuleSerializer.DeserializeMany(
            """[{"type":"rowFilter","type":"x","version":1,"action":"include","conditions":[]}]"""));

    [Theory]
    [InlineData("""{"type":"rowFilter","version":1,"action":7,"conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":0,"conditions":[]}""")]
    [InlineData("""{"type":"rowFilter","version":1,"action":"include","conditions":[{"field":"A","operator":1}]}""")]
    [InlineData("""{"type":"transform","version":1,"field":"A","kind":1}""")]
    public void Deserialize_NumericEnumValue_ShouldThrowRuleSerializationException(string json) =>
        Should.Throw<RuleSerializationException>(() => RuleSerializer.Deserialize(json));

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[1]")]
    [InlineData("not json")]
    public void DeserializeMany_BadInput_ShouldOnlyThrowRuleSerializationException(string json) =>
        Should.Throw<RuleSerializationException>(() => RuleSerializer.DeserializeMany(json));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_BlankInput_ShouldThrowArgumentException(string json) =>
        Should.Throw<ArgumentException>(() => RuleSerializer.Deserialize(json));

    [Fact]
    public void Deserialize_ShouldPreserveCyrillicEmojiAndSpecialChars()
    {
        var rule = new DefaultValueRule { TargetField = "Град \"Ћ\"", Value = "Ђорђе 😀 <script>'; DROP TABLE x;--" };

        var restored = RuleSerializer.Deserialize(RuleSerializer.Serialize(rule)).ShouldBeOfType<DefaultValueRule>();

        restored.TargetField.ShouldBe(rule.TargetField);
        restored.Value.ShouldBe(rule.Value);
    }

    [Fact]
    public void Deserialize_ShouldAcceptNullOptionalFieldsAndEmptyConditions()
    {
        var rule = RuleSerializer.Deserialize(
            """{"type":"conditionalValue","version":1,"targetField":"A","conditions":[],"value":null,"elseValue":null}""");

        rule.ShouldBeOfType<ConditionalValueRule>().Conditions.ShouldBeEmpty();
    }

    // Matrica: operator × tip ćelije. Nijedna kombinacija ne sme da baci izuzetak.
    public static TheoryData<ComparisonOperator, object?> OperatorByCellType()
    {
        object?[] cells =
        [
            null, "", "  ", "abc", "Ђорђе", "😀", "12", "-0", "1e3", "1,5", "NaN", "Infinity",
            0, 5, -5, long.MaxValue, ulong.MaxValue, 5m, decimal.MaxValue, decimal.MinValue,
            1.5, 0.1, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, float.NaN, 1.5f,
            true, false, 'x', new DateTime(2024, 1, 1), DateOnly.MinValue, DateTimeOffset.MaxValue, TimeSpan.FromHours(1), Guid.Empty,
            new object(),
        ];
        var data = new TheoryData<ComparisonOperator, object?>();
        foreach (var op in Enum.GetValues<ComparisonOperator>())
        {
            foreach (var cell in cells)
            {
                data.Add(op, cell);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(OperatorByCellType))]
    public void Evaluate_ShouldNeverThrow_ForAnyOperatorAndCellType(ComparisonOperator op, object? cell)
    {
        foreach (var expected in new string?[] { null, "", " ", "5", "abc", "1e3", "NaN", "99999999999999999999999999999999", "2024-01-01", "0001-01-01", "😀" })
        {
            Should.NotThrow(() => ConditionEvaluator.Evaluate(op, cell, expected));
        }
    }

    [Fact]
    public void Evaluate_Eq_And_Neq_ShouldBeComplementary()
    {
        object?[] cells = [null, "", "a", "5", 5, 5.0, true, new DateTime(2024, 1, 1)];
        foreach (var cell in cells)
        {
            foreach (var expected in new string?[] { null, "", "a", "5", "2024-01-01" })
            {
                ConditionEvaluator.Evaluate(Eq, cell, expected).ShouldBe(!ConditionEvaluator.Evaluate(Neq, cell, expected));
            }
        }
    }

    [Theory]
    [InlineData(true, "true", true)]
    [InlineData(true, "TRUE", true)]
    [InlineData(false, "true", false)]
    [InlineData(1, "1.0", true)]
    [InlineData("0", "-0", true)]
    [InlineData("1e3", "1000", true)]
    [InlineData("9", "10", false)] // numerički, ne leksikografski
    public void Evaluate_Eq_MixedTypes(object cell, string expected, bool result) =>
        ConditionEvaluator.Evaluate(Eq, cell, expected).ShouldBe(result);

    [Fact]
    public void Evaluate_Contains_WithEmptyExpected_IsTrueForAnyNonNullCell() =>
        ConditionEvaluator.Evaluate(Contains, "abc", "").ShouldBeTrue();

    [Fact]
    public void Evaluate_ShouldBeCultureInvariant()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("sr-Latn-RS");
            ConditionEvaluator.Evaluate(Eq, 1.5, "1.5").ShouldBeTrue();
            ConditionEvaluator.Evaluate(Contains, 1.5, "1.5").ShouldBeTrue();
            ConditionEvaluator.Evaluate(Gt, "2024-12-01", "2024-03-15").ShouldBeTrue();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Evaluate_ShouldThrow_OnNullArguments()
    {
        Should.Throw<ArgumentNullException>(() => ConditionEvaluator.Evaluate(null!, new Dictionary<string, object?>()));
        Should.Throw<ArgumentNullException>(() => ConditionEvaluator.Evaluate(new Condition("A", IsEmpty), null!));
    }
}

