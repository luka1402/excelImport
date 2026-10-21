using System.Text.Json;
using ExcelImporter.Application.Mapping;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Templates;

/// <summary>JSON zapis sadržaja šablona u tri kolone (mapiranje, pravila, konstante); mapiranje nosi i kolone izvornog fajla.</summary>
public static class TemplateContentSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static (string MappingJson, string RulesJson, string ConstantsJson) Serialize(TemplateContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return (
            JsonSerializer.Serialize(new MappingEnvelope([.. content.SourceColumns], [.. content.Mappings]), Options),
            RuleSerializer.Serialize(content.Rules),
            JsonSerializer.Serialize(content.Constants, Options));
    }

    /// <exception cref="TemplateInvalidException">Sačuvani JSON nije čitljiv (oštećen ili nepoznata verzija pravila).</exception>
    public static TemplateContent Deserialize(string mappingJson, string rulesJson, string constantsJson)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MappingEnvelope>(mappingJson, Options)
                ?? throw new JsonException("Mapiranje je prazno.");
            return Parse(envelope.SourceColumns, envelope.Mappings, rulesJson, constantsJson);
        }
        catch (Exception exception) when (exception is JsonException or RuleSerializationException or ArgumentException)
        {
            throw new TemplateInvalidException([$"Sačuvani sadržaj šablona nije čitljiv: {exception.Message}"]);
        }
    }

    /// <summary>Sklapa sadržaj iz delova koji stižu kao JSON (pravila i konstante); koristi i API pri čitanju zahteva.</summary>
    /// <exception cref="JsonException">Neispravan JSON konstanti.</exception>
    /// <exception cref="RuleSerializationException">Neispravan JSON pravila.</exception>
    public static TemplateContent Parse(
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<ColumnMapping> mappings,
        string rulesJson,
        string constantsJson)
    {
        // Ručno izmenjen JSON ili telo zahteva mogu imati null elemente koje deserijalizator ne odbija.
        if (sourceColumns is null || sourceColumns.Any(column => column is null))
        {
            throw new JsonException("Kolone izvornog fajla ne smeju biti prazne.");
        }

        if (mappings is null || mappings.Any(mapping => mapping is null || mapping.SourceColumn is null || mapping.TargetColumn is null))
        {
            throw new JsonException("Svako mapiranje mora imati sourceColumn i targetColumn.");
        }

        var rules = RuleSerializer.DeserializeMany(rulesJson);
        var constants = JsonSerializer.Deserialize<List<ColumnConstant>>(constantsJson, Options)
            ?? throw new JsonException("Konstante su prazne.");
        if (constants.Any(constant => constant is null || constant.TargetColumn is null || constant.Value is null))
        {
            throw new JsonException("Svaka konstanta mora imati targetColumn i value.");
        }

        return new TemplateContent(sourceColumns, mappings, rules, constants);
    }

    private sealed record MappingEnvelope(List<string> SourceColumns, List<ColumnMapping> Mappings);
}