namespace ExcelImporter.Domain.Rules;

/// <summary>Registar poznatih tipova pravila i podržanih verzija njihovog JSON oblika.</summary>
public static class RuleTypes
{
    public const int CurrentVersion = 1;

    public const string ConditionalValue = "conditionalValue";
    public const string DefaultValue = "defaultValue";
    public const string Transform = "transform";
    public const string Lookup = "lookup";
    public const string RowFilter = "rowFilter";

    private static readonly Dictionary<string, (Type Clr, int[] Versions)> Known = new(StringComparer.Ordinal)
    {
        [ConditionalValue] = (typeof(ConditionalValueRule), [1]),
        [DefaultValue] = (typeof(DefaultValueRule), [1]),
        [Transform] = (typeof(TransformRule), [1]),
        [Lookup] = (typeof(LookupRule), [1]),
        [RowFilter] = (typeof(RowFilterRule), [1]),
    };

    public static IReadOnlyCollection<string> All => Known.Keys;

    public static bool TryGetClrType(string type, int version, out Type clrType)
    {
        if (Known.TryGetValue(type, out var entry) && entry.Versions.Contains(version))
        {
            clrType = entry.Clr;
            return true;
        }

        clrType = typeof(Rule);
        return false;
    }

    public static bool IsKnownType(string type) => Known.ContainsKey(type);

    public static bool IsSupportedVersion(string type, int version) =>
        Known.TryGetValue(type, out var entry) && entry.Versions.Contains(version);
}