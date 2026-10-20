using System.Text.Json.Serialization;

namespace ExcelImporter.Domain.Rules;

/// <summary>Operatori poreÄ‘enja u uslovima pravila. JSON vrednosti su stabilan ugovor.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ComparisonOperator>))]
public enum ComparisonOperator
{
    [JsonStringEnumMemberName("eq")] Eq,
    [JsonStringEnumMemberName("neq")] Neq,
    [JsonStringEnumMemberName("gt")] Gt,
    [JsonStringEnumMemberName("lt")] Lt,
    [JsonStringEnumMemberName("gte")] Gte,
    [JsonStringEnumMemberName("lte")] Lte,
    [JsonStringEnumMemberName("contains")] Contains,
    [JsonStringEnumMemberName("startsWith")] StartsWith,
    [JsonStringEnumMemberName("isEmpty")] IsEmpty,
    [JsonStringEnumMemberName("isNotEmpty")] IsNotEmpty,
}