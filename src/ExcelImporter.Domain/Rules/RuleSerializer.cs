using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ExcelImporter.Domain.Rules;

/// <summary>Greška čitanja pravila iz JSON-a (nepoznat tip/verzija, neispravan oblik).</summary>
public sealed class RuleSerializationException : Exception
{
    public RuleSerializationException(string message) : base(message) { }

    public RuleSerializationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>JSON serijalizacija pravila; tip se bira po polju "type", a oblik po polju "version".</summary>
public static class RuleSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // Odbija null za non-nullable članove i nedostajuće obavezne parametre konstruktora (npr. Condition.Field).
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static string Serialize(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return JsonSerializer.Serialize(rule, rule.GetType(), Options);
    }

    public static string Serialize(IEnumerable<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var nodes = rules.Select(rule => JsonSerializer.SerializeToNode(rule, rule.GetType(), Options));
        return new JsonArray([.. nodes]).ToJsonString(Options);
    }

    public static Rule Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            return ReadRule(JsonNode.Parse(json));
        }
        // JsonNode baca ArgumentException (a ne JsonException) za duplirani ključ, i to lenjo pri pristupu svojstvu.
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new RuleSerializationException($"Pravilo nije ispravan JSON: {exception.Message}", exception);
        }
    }

    public static IReadOnlyList<Rule> DeserializeMany(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            if (JsonNode.Parse(json) is not JsonArray array)
            {
                throw new RuleSerializationException("Očekivan je JSON niz pravila.");
            }

            return [.. array.Select(ReadRule)];
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new RuleSerializationException($"Pravila nisu ispravan JSON: {exception.Message}", exception);
        }
    }

    private static Rule ReadRule(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            throw new RuleSerializationException("Pravilo mora biti JSON objekat.");
        }

        var type = obj["type"]?.GetValueKind() == JsonValueKind.String ? obj["type"]!.GetValue<string>() : null;
        if (type is null)
        {
            throw new RuleSerializationException("Pravilu nedostaje polje \"type\".");
        }

        if (!RuleTypes.IsKnownType(type))
        {
            throw new RuleSerializationException(
                $"Nepoznat tip pravila \"{type}\". Podržani tipovi: {string.Join(", ", RuleTypes.All)}.");
        }

        if (obj["version"]?.GetValueKind() != JsonValueKind.Number || !obj["version"]!.AsValue().TryGetValue<int>(out var version))
        {
            throw new RuleSerializationException($"Pravilu tipa \"{type}\" nedostaje celobrojno polje \"version\".");
        }

        if (!RuleTypes.TryGetClrType(type, version, out var clrType))
        {
            throw new RuleSerializationException(
                $"Verzija {version} pravila tipa \"{type}\" nije podržana (najnovija je {RuleTypes.CurrentVersion}).");
        }

        // "type" je izračunato svojstvo, pa ga uklanjamo da ne bi bilo nemapirano polje.
        obj.Remove("type");
        try
        {
            var rule = (Rule)(obj.Deserialize(clrType, Options)
                ?? throw new RuleSerializationException($"Pravilo tipa \"{type}\" je prazno."));
            EnsureConditionsAreComplete(rule, type);
            return rule;
        }
        catch (JsonException exception)
        {
            throw new RuleSerializationException($"Pravilo tipa \"{type}\" nije ispravno: {exception.Message}", exception);
        }
    }

    // Nullable anotacije ne pokrivaju elemente kolekcije ni propuštene parametre konstruktora u svim slučajevima.
    private static void EnsureConditionsAreComplete(Rule rule, string type)
    {
        var conditions = rule switch
        {
            ConditionalValueRule conditional => conditional.Conditions,
            RowFilterRule filter => filter.Conditions,
            _ => null,
        };

        if (conditions is null)
        {
            if (rule is ConditionalValueRule or RowFilterRule)
            {
                throw new RuleSerializationException($"Pravilu tipa \"{type}\" nedostaje polje \"conditions\".");
            }

            return;
        }

        foreach (var condition in conditions)
        {
            if (condition is null || string.IsNullOrEmpty(condition.Field))
            {
                throw new RuleSerializationException(
                    $"Svaki uslov pravila tipa \"{type}\" mora biti objekat sa poljem \"field\".");
            }
        }
    }
}