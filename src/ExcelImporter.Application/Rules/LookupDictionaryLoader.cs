using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;

namespace ExcelImporter.Application.Rules;

/// <summary>Čita parove (ključ, povratna vrednost) iz ciljne baze. Imena su već proverena po šemi (whitelist) i kanonska.</summary>
public interface ILookupDataSource
{
    Task<IReadOnlyList<KeyValuePair<string, object?>>> ReadEntriesAsync(
        string connectionString,
        string schemaName,
        string tableName,
        string keyColumn,
        string returnColumn,
        CancellationToken cancellationToken);
}

/// <summary>Učitava rečnik lookup pravila. Poziva se jednom po uvozu i pravilu; rezultat se koristi za sve redove.</summary>
public interface ILookupDictionaryLoader
{
    /// <exception cref="LookupConfigurationException">Tabela/kolona ne postoji u ciljnoj bazi ili ključna kolona ima duplikate.</exception>
    Task<LookupMap> LoadAsync(string connectionString, LookupRule rule, CancellationToken cancellationToken);
}

public sealed class LookupDictionaryLoader(ISchemaProvider schemaProvider, ILookupDataSource dataSource) : ILookupDictionaryLoader
{
    public async Task<LookupMap> LoadAsync(string connectionString, LookupRule rule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var schema = await schemaProvider.GetSchemaAsync(connectionString, cancellationToken);
        var description = $"{rule.Schema}.{rule.Table} ({rule.KeyColumn} → {rule.ReturnColumn})";

        var table = schema.FindTable(rule.Schema, rule.Table)
            ?? throw new LookupConfigurationException($"Lookup {description}: tabela ne postoji u ciljnoj bazi.");
        var keyColumn = table.FindColumn(rule.KeyColumn)
            ?? throw new LookupConfigurationException($"Lookup {description}: ključna kolona \"{rule.KeyColumn}\" ne postoji.");
        var returnColumn = table.FindColumn(rule.ReturnColumn)
            ?? throw new LookupConfigurationException($"Lookup {description}: povratna kolona \"{rule.ReturnColumn}\" ne postoji.");

        var entries = await dataSource.ReadEntriesAsync(
            connectionString, table.Schema, table.Name, keyColumn.Name, returnColumn.Name, cancellationToken);

        return new LookupMap(entries, description);
    }
}
