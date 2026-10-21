using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Schema;

namespace ExcelImporter.Application.Templates;

/// <summary>Primena sačuvanog šablona na kolone novog fajla.</summary>
public static class TemplateApplicator
{
    /// <param name="template">Sačuvani šablon.</param>
    /// <param name="fileColumns">Nazivi kolona novog fajla (header).</param>
    /// <param name="schema">Aktuelna šema ciljne baze; šablon koji je prestao da važi prema njoj se označava, ne primenjuje.</param>
    public static TemplateApplication Apply(SavedTemplate template, IReadOnlyList<string> fileColumns, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(fileColumns);
        ArgumentNullException.ThrowIfNull(schema);

        var fileByNormalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var column in fileColumns)
        {
            fileByNormalized.TryAdd(ColumnNameNormalizer.Normalize(column), column);
        }

        var differences = ColumnsFingerprint.Compute(fileColumns) == template.ColumnsFingerprint
            ? new ColumnDifferences([], [])
            : Compare(template.Content.SourceColumns, fileColumns);

        // Izvorna kolona se traži po normalizovanom nazivu, da "Šifra Kupca" i "sifra_kupca" ostanu isto mapiranje.
        var mappings = new List<ColumnMapping>();
        foreach (var mapping in template.Content.Mappings)
        {
            if (fileByNormalized.TryGetValue(ColumnNameNormalizer.Normalize(mapping.SourceColumn), out var actual))
            {
                mappings.Add(mapping with { SourceColumn = actual });
            }
        }

        var schemaIssues = TemplateValidator.ValidateContent(
            template.SchemaName,
            template.TableName,
            template.Content with { SourceColumns = [.. template.Content.SourceColumns] },
            schema);

        return new TemplateApplication(template, mappings, differences, schemaIssues);
    }

    private static ColumnDifferences Compare(IReadOnlyList<string> templateColumns, IReadOnlyList<string> fileColumns)
    {
        var templateNames = templateColumns.Select(ColumnNameNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);
        var fileNames = fileColumns.Select(ColumnNameNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);

        return new ColumnDifferences(
            [.. fileColumns.Where(column => !templateNames.Contains(ColumnNameNormalizer.Normalize(column)))],
            [.. templateColumns.Where(column => !fileNames.Contains(ColumnNameNormalizer.Normalize(column)))]);
    }
}