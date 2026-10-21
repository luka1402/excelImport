using ExcelImporter.Application.Rules;
using Microsoft.Data.SqlClient;

namespace ExcelImporter.Infrastructure.Schema;

/// <summary>
/// Čita ključnu i povratnu kolonu jedne tabele. Imena objekata ne mogu se parametrizovati, pa se oslanjamo na
/// <see cref="LookupDictionaryLoader"/> (whitelist po šemi) i dodatno ih citiramo kao [ime] uz udvajanje "]".
/// </summary>
public sealed class SqlServerLookupDataSource : ILookupDataSource
{
    public async Task<IReadOnlyList<KeyValuePair<string, object?>>> ReadEntriesAsync(
        string connectionString,
        string schemaName,
        string tableName,
        string keyColumn,
        string returnColumn,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT {Quote(keyColumn)}, {Quote(returnColumn)} FROM {Quote(schemaName)}.{Quote(tableName)} WHERE {Quote(keyColumn)} IS NOT NULL";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var entries = new List<KeyValuePair<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            entries.Add(new(key, reader.IsDBNull(1) ? null : reader.GetValue(1)));
        }

        return entries;
    }

    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
