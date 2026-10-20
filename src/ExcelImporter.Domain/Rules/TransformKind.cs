using System.Text.Json.Serialization;

namespace ExcelImporter.Domain.Rules;

[JsonConverter(typeof(StrictStringEnumConverter<TransformKind>))]
public enum TransformKind
{
    [JsonStringEnumMemberName("trim")] Trim,
    [JsonStringEnumMemberName("upper")] Upper,
    [JsonStringEnumMemberName("lower")] Lower,
    [JsonStringEnumMemberName("dateFormat")] DateFormat,
    [JsonStringEnumMemberName("decimalSeparator")] DecimalSeparator,
}

[JsonConverter(typeof(StrictStringEnumConverter<RowFilterAction>))]
public enum RowFilterAction
{
    [JsonStringEnumMemberName("include")] Include,
    [JsonStringEnumMemberName("exclude")] Exclude,
}