namespace ExcelImporter.Domain.Rules;

/// <summary>Uslov nad jednom kolonom reda. <see cref="Value"/> se ne koristi za isEmpty / isNotEmpty.</summary>
public sealed record Condition(string Field, ComparisonOperator Operator, string? Value = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresValue => Operator is not (ComparisonOperator.IsEmpty or ComparisonOperator.IsNotEmpty);
}