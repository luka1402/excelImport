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
        catch (Exception exception) when (exception is JsonException or RuleSerializationException)
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
        var rules = RuleSerializer.DeserializeMany(rulesJson);
        var constants = JsonSerializer.Deserialize<List<ColumnConstant>>(constantsJson, Options)
            ?? throw new JsonException("Konstante su prazne.");
        return new TemplateContent(sourceColumns, mappings, rules, constants);
    }

    private sealed record MappingEnvelope(List<string> SourceColumns, List<ColumnMapping> Mappings);
}