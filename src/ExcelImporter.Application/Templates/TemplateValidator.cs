using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Templates;

/// <summary>
/// Proverava sadržaj šablona protiv aktuelne šeme ciljne baze (whitelist): tabela, ciljne kolone mapiranja, konstanti i pravila
/// i tabele/kolone lookup pravila moraju postojati. Isti postupak koristi čuvanje (odbija) i primena (označava).
/// </summary>
public static class TemplateValidator
{
    public const int MaxNameLength = 200;
    public const int MaxSheetNameLength = 255;

    private static readonly RuleValidator RuleChecker = new();

    public static IReadOnlyList<string> ValidateDraft(TemplateDraft draft, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(schema);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            errors.Add("Ime šablona je obavezno.");
        }
        else if (draft.Name.Trim().Length > MaxNameLength)
        {
            errors.Add($"Ime šablona može imati najviše {MaxNameLength} karaktera.");
        }

        if (draft.HeaderRow < 1)
        {
            errors.Add("Red zaglavlja mora biti 1 ili veći.");
        }

        if (draft.SheetName is { Length: > MaxSheetNameLength })
        {
            errors.Add($"Naziv sheet-a može imati najviše {MaxSheetNameLength} karaktera.");
        }

        errors.AddRange(ValidateContent(draft.SchemaName, draft.TableName, draft.Content, schema));
        return errors;
    }

    public static IReadOnlyList<string> ValidateContent(string schemaName, string tableName, TemplateContent content, DatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(schema);

        var table = schema.FindTable(schemaName, tableName);
        if (table is null)
        {
            return [$"Tabela \"{schemaName}.{tableName}\" ne postoji u ciljnoj bazi."];
        }

        var errors = new List<string>();
        errors.AddRange(MappingValidator.Validate(schemaName, tableName, content.Mappings, schema, content.SourceColumns)
            .Errors.Select(error => error.Message));
        errors.AddRange(ColumnConstants.Validate(table, content.Constants, content.Mappings.Select(mapping => mapping.TargetColumn))
            .Select(error => error.Message));

        foreach (var rule in content.Rules)
        {
            errors.AddRange(ValidateRule(rule, table, schema));
        }

        return errors;
    }

    private static IEnumerable<string> ValidateRule(Rule rule, TableSchema table, DatabaseSchema schema)
    {
        foreach (var failure in RuleChecker.Validate(rule).Errors)
        {
            yield return $"Pravilo \"{rule.Type}\": {failure.ErrorMessage}";
        }

        var targetField = rule switch
        {
            ConditionalValueRule r => r.TargetField,
            DefaultValueRule r => r.TargetField,
            TransformRule r => r.TargetField,
            LookupRule r => r.TargetField,
            _ => null,
        };

        if (!string.IsNullOrEmpty(targetField) && table.FindColumn(targetField) is null)
        {
            yield return $"Pravilo \"{rule.Type}\": ciljna kolona \"{targetField}\" ne postoji u tabeli \"{table.Schema}.{table.Name}\".";
        }

        if (rule is not LookupRule lookup)
        {
            yield break;
        }

        var lookupTable = schema.FindTable(lookup.Schema, lookup.Table);
        if (lookupTable is null)
        {
            yield return $"Lookup pravilo: tabela \"{lookup.Schema}.{lookup.Table}\" ne postoji u ciljnoj bazi.";
            yield break;
        }

        foreach (var column in new[] { lookup.KeyColumn, lookup.ReturnColumn })
        {
            if (!string.IsNullOrEmpty(column) && lookupTable.FindColumn(column) is null)
            {
                yield return $"Lookup pravilo: kolona \"{column}\" ne postoji u tabeli \"{lookup.Schema}.{lookup.Table}\".";
            }
        }
    }
}