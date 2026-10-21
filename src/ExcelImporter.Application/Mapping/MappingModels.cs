namespace ExcelImporter.Application.Mapping;

/// <summary>Mapiranje jedne kolone fajla (po nazivu iz header-a) na kolonu ciljne tabele.</summary>
public sealed record ColumnMapping(string SourceColumn, string TargetColumn);

public enum MatchKind
{
    /// <summary>Nazivi su isti posle normalizacije (velika/mala slova, razmaci, dijakritici).</summary>
    Exact,

    /// <summary>Nazivi su slični (mala razlika u kucanju), ali nisu isti.</summary>
    Similar,
}

public sealed record MappingSuggestion(string SourceColumn, string TargetColumn, MatchKind Match);

public enum MappingWarningKind
{
    TypeMismatch,
    RequiredColumnUnmapped,
    TextTooLong,
}

/// <summary>
/// Upozorenje o mapiranju. <see cref="SourceColumn"/> je null za nemapiranu obaveznu kolonu.
/// Za <see cref="MappingWarningKind.TextTooLong"/> nose broj i primer prekoračenja.
/// </summary>
public sealed record MappingWarning(
    MappingWarningKind Kind,
    string? SourceColumn,
    string TargetColumn,
    string Message,
    int? AffectedRowCount = null,
    int? FirstAffectedRow = null,
    int? LongestValueLength = null);

public sealed record MappingError(string? TargetColumn, string Message);

public sealed record MappingValidationResult(IReadOnlyList<MappingError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
