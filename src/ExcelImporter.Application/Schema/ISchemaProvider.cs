namespace ExcelImporter.Application.Schema;

/// <summary>Čita šemu ciljne baze direktno iz sistemskog kataloga (bez keša).</summary>
public interface ISchemaReader
{
    Task<DatabaseSchema> ReadAsync(string connectionString, CancellationToken cancellationToken);
}

/// <summary>Šema ciljne baze sa keširanjem po konekciji. Poziva se pre svake upotrebe naziva iz korisničkog unosa.</summary>
public interface ISchemaProvider
{
    /// <summary>Vraća keširanu šemu; učitava je ako je nema ili je istekla.</summary>
    Task<DatabaseSchema> GetSchemaAsync(string connectionString, CancellationToken cancellationToken);

    /// <summary>Odbacuje keš za konekciju i ponovo učitava šemu (npr. posle DDL promene na ciljnoj bazi).</summary>
    Task<DatabaseSchema> RefreshAsync(string connectionString, CancellationToken cancellationToken);
}

public static class SchemaProviderExtensions
{
    /// <summary>Provera "postoji li šema.tabela.kolona" nad (keširanom) šemom ciljne baze.</summary>
    public static async Task<bool> ColumnExistsAsync(
        this ISchemaProvider provider,
        string connectionString,
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var schema = await provider.GetSchemaAsync(connectionString, cancellationToken);
        return schema.ColumnExists(schemaName, tableName, columnName);
    }

    public static async Task<bool> TableExistsAsync(
        this ISchemaProvider provider,
        string connectionString,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var schema = await provider.GetSchemaAsync(connectionString, cancellationToken);
        return schema.TableExists(schemaName, tableName);
    }
}
