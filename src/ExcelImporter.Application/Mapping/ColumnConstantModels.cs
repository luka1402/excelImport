namespace ExcelImporter.Application.Mapping;

public enum DynamicConstantKind
{
    /// <summary>Današnji datum (bez vremena), računa se u trenutku uvoza.</summary>
    Today,

    /// <summary>Tekući datum i vreme, računa se u trenutku uvoza.</summary>
    Now,
}

/// <summary>
/// Konstantna vrednost za ciljnu kolonu koja nije mapirana iz fajla: ili literal (tekst koji se konvertuje u tip kolone)
/// ili dinamička vrednost ("danas"/"sada"). Tačno jedno od <see cref="Literal"/> i <see cref="Dynamic"/> mora biti zadato.
/// </summary>
public sealed record ConstantValue
{
    public string? Literal { get; init; }

    public DynamicConstantKind? Dynamic { get; init; }

    public static ConstantValue FromLiteral(string literal) => new() { Literal = literal };

    public static ConstantValue FromDynamic(DynamicConstantKind kind) => new() { Dynamic = kind };
}

/// <summary>Konstanta za ciljnu kolonu (npr. Status = "Aktivan", DatumUvoza = danas).</summary>
public sealed record ColumnConstant(string TargetColumn, ConstantValue Value);
