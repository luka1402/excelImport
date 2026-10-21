using ExcelImporter.Application.Schema;

namespace ExcelImporter.Application.Mapping;

/// <summary>
/// Validira mapiranje koje stiže od korisnika protiv whitelist-a (šema ciljne baze) pre nego što bilo koji naziv uđe u SQL.
/// Odbija nepostojeću tabelu i kolonu, identity/computed kolonu, ciljnu kolonu mapiranu dvaput i izvornu kolonu koje nema u fajlu.
/// </summary>
public static class MappingValidator
{
    public static MappingValidationResult Validate(
        string schemaName,
        string tableName,
        IReadOnlyList<ColumnMapping> mappings,
        DatabaseSchema schema,
        IEnumerable<string> sourceColumnNames)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(sourceColumnNames);

        var table = schema.FindTable(schemaName, tableName);
        if (table is null)
        {
            return new MappingValidationResult([new MappingError(null, $"Tabela \"{schemaName}.{tableName}\" ne postoji u ciljnoj bazi.")]);
        }

        var sources = new HashSet<string>(sourceColumnNames, StringComparer.Ordinal);
        var mappedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<MappingError>();

        foreach (var mapping in mappings)
        {
            if (!sources.Contains(mapping.SourceColumn))
            {
                errors.Add(new MappingError(mapping.TargetColumn, $"Kolona \"{mapping.SourceColumn}\" ne postoji u fajlu."));
            }

            var column = table.FindColumn(mapping.TargetColumn);
            if (column is null)
            {
                errors.Add(new MappingError(mapping.TargetColumn, $"Kolona \"{mapping.TargetColumn}\" ne postoji u tabeli \"{table.Schema}.{table.Name}\"."));
                continue;
            }

            if (column.IsIdentity || column.IsComputed)
            {
                errors.Add(new MappingError(column.Name, $"Kolona \"{column.Name}\" je {(column.IsIdentity ? "identity" : "computed")} i ne može se puniti iz fajla."));
            }

            if (!mappedTargets.Add(column.Name))
            {
                errors.Add(new MappingError(column.Name, $"Kolona \"{column.Name}\" je mapirana više puta."));
            }
        }

        return new MappingValidationResult(errors);
    }
}
