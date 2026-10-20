using System.Text.Json.Serialization;

namespace ExcelImporter.Domain.Rules;

/// <summary>Enum se čita isključivo kao string; numeričke vrednosti (npr. 7) se odbijaju.</summary>
public sealed class StrictStringEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;
