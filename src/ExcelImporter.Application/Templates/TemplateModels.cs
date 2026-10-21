using ExcelImporter.Application.Mapping;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Templates;

/// <summary>Sadržaj šablona: mapiranje, pravila i konstante, plus kolone fajla iz kojeg je šablon nastao.</summary>
public sealed record TemplateContent(
    IReadOnlyList<string> SourceColumns,
    IReadOnlyList<ColumnMapping> Mappings,
    IReadOnlyList<Rule> Rules,
    IReadOnlyList<ColumnConstant> Constants);

/// <summary>Zahtev za čuvanje šablona (novog ili izmene postojećeg).</summary>
public sealed record TemplateDraft(
    string Name,
    int ConnectionId,
    string SchemaName,
    string TableName,
    string? SheetName,
    int HeaderRow,
    TemplateContent Content);

public sealed record SavedTemplate(
    int Id,
    string Name,
    int ConnectionId,
    string SchemaName,
    string TableName,
    string? SheetName,
    int HeaderRow,
    string ColumnsFingerprint,
    TemplateContent Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record TemplateSummary(
    int Id,
    string Name,
    int ConnectionId,
    string SchemaName,
    string TableName,
    DateTime UpdatedAt);

/// <summary>Razlike između kolona fajla iz kog je šablon nastao i kolona novog fajla.</summary>
public sealed record ColumnDifferences(IReadOnlyList<string> NewColumns, IReadOnlyList<string> MissingColumns)
{
    public bool HasDifferences => NewColumns.Count > 0 || MissingColumns.Count > 0;
}

/// <summary>
/// Rezultat primene šablona na novi fajl. <see cref="Mappings"/> sadrži samo mapiranja čija izvorna kolona postoji u fajlu;
/// <see cref="SchemaIssues"/> nisu prazne kada šablon ne važi više prema aktuelnoj šemi ciljne baze (šablon je tada označen kao neupotrebljiv).
/// </summary>
public sealed record TemplateApplication(
    SavedTemplate Template,
    IReadOnlyList<ColumnMapping> Mappings,
    ColumnDifferences Differences,
    IReadOnlyList<string> SchemaIssues)
{
    public bool IsUsable => SchemaIssues.Count == 0;

    /// <summary>Isti fajl (isti otisak kolona) ne daje upozorenje; izmenjene kolone daju spisak.</summary>
    public bool HasColumnWarning => Differences.HasDifferences;
}

public sealed class DuplicateTemplateNameException(string name)
    : Exception($"Šablon sa imenom \"{name}\" već postoji.");

public sealed class TemplateNotFoundException(int id)
    : Exception($"Šablon {id} ne postoji.");

public sealed class TemplateInvalidException(IReadOnlyList<string> errors)
    : Exception("Šablon nije ispravan: " + string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}