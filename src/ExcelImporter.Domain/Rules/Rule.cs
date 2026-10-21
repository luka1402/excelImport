using System.Text.Json.Serialization;

namespace ExcelImporter.Domain.Rules;

/// <summary>
/// Osnova svih pravila. <see cref="Type"/> i <see cref="Version"/> određuju oblik JSON-a;
/// nove verzije oblika dodaju se u <see cref="RuleTypes"/>.
/// </summary>
public abstract record Rule
{
    public abstract string Type { get; }

    [JsonPropertyOrder(-1)]
    public int Version { get; init; } = RuleTypes.CurrentVersion;
}

/// <summary>Postavlja vrednost ciljne kolone ako su svi uslovi ispunjeni, inače ElseValue (ako postoji).</summary>
public sealed record ConditionalValueRule : Rule
{
    public override string Type => RuleTypes.ConditionalValue;
    public required string TargetField { get; init; }
    public required IReadOnlyList<Condition> Conditions { get; init; }
    public string? Value { get; init; }
    public string? ElseValue { get; init; }
}

/// <summary>Postavlja podrazumevanu vrednost u ciljnu kolonu kada je ona prazna.</summary>
public sealed record DefaultValueRule : Rule
{
    public override string Type => RuleTypes.DefaultValue;
    public required string TargetField { get; init; }
    public required string Value { get; init; }
}

/// <summary>Primenjuje transformaciju teksta na ciljnu kolonu.</summary>
public sealed record TransformRule : Rule
{
    public override string Type => RuleTypes.Transform;
    public required string TargetField { get; init; }
    public required TransformKind Kind { get; init; }

    /// <summary>Ulazni format datuma (npr. dd.MM.yyyy); obavezan za <see cref="TransformKind.DateFormat"/>.</summary>
    public string? Format { get; init; }

    /// <summary>Decimalni separator ulaznog broja ("," ili "."); obavezan za <see cref="TransformKind.DecimalSeparator"/>.</summary>
    public string? Separator { get; init; }
}

/// <summary>
/// Strani ključ: vrednost izvorne (Excel) kolone traži se u ključnoj koloni tabele ciljne baze,
/// a u <see cref="TargetField"/> upisuje se vrednost povratne kolone (npr. "Novi Sad" → GradId).
/// </summary>
public sealed record LookupRule : Rule
{
    public override string Type => RuleTypes.Lookup;
    public required string TargetField { get; init; }
    public required string SourceField { get; init; }
    public string Schema { get; init; } = "dbo";
    public required string Table { get; init; }
    public required string KeyColumn { get; init; }
    public required string ReturnColumn { get; init; }
}

/// <summary>Uključuje ili isključuje redove za koje su svi uslovi ispunjeni.</summary>
public sealed record RowFilterRule : Rule
{
    public override string Type => RuleTypes.RowFilter;
    public required RowFilterAction Action { get; init; }
    public required IReadOnlyList<Condition> Conditions { get; init; }
}