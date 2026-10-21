using Dapper;
using ExcelImporter.Application.Schema;
using Microsoft.Data.SqlClient;

namespace ExcelImporter.Infrastructure.Schema;

/// <summary>Čita tabele, kolone, PK i FK iz sistemskog kataloga SQL Servera. Sistemske šeme i objekti (sys, INFORMATION_SCHEMA, is_ms_shipped) se ne vide.</summary>
public sealed class SqlServerSchemaReader : ISchemaReader
{
    private static readonly string[] ExcludedSchemas = ["sys", "INFORMATION_SCHEMA"];

    // Nazivi dolaze iz kataloga, ne iz korisničkog unosa; filteri su parametrizovani.
    private const string ColumnsSql = """
        SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName, c.column_id AS Ordinal,
               TYPE_NAME(c.system_type_id) AS DataType, c.is_nullable AS IsNullable,
               c.max_length AS MaxLengthBytes, c.precision AS [Precision], c.scale AS Scale,
               c.is_identity AS IsIdentity, c.is_computed AS IsComputed
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        WHERE t.is_ms_shipped = 0 AND s.name NOT IN @ExcludedSchemas
        ORDER BY s.name, t.name, c.column_id
        """;

    private const string PrimaryKeysSql = """
        SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.indexes i ON i.object_id = t.object_id AND i.is_primary_key = 1
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE t.is_ms_shipped = 0 AND s.name NOT IN @ExcludedSchemas
        ORDER BY s.name, t.name, ic.key_ordinal
        """;

    private const string ForeignKeysSql = """
        SELECT ps.name AS SchemaName, pt.name AS TableName, fk.name AS ConstraintName,
               pc.name AS ColumnName, rs.name AS ReferencedSchema, rt.name AS ReferencedTable, rc.name AS ReferencedColumn
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
        JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
        JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
        JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        WHERE pt.is_ms_shipped = 0 AND rt.is_ms_shipped = 0
          AND ps.name NOT IN @ExcludedSchemas AND rs.name NOT IN @ExcludedSchemas
        ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id
        """;

    public async Task<DatabaseSchema> ReadAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var parameters = new { ExcludedSchemas };
        var columns = (await connection.QueryAsync<ColumnRow>(new CommandDefinition(ColumnsSql, parameters, cancellationToken: cancellationToken))).AsList();
        var primaryKeys = (await connection.QueryAsync<PrimaryKeyRow>(new CommandDefinition(PrimaryKeysSql, parameters, cancellationToken: cancellationToken))).AsList();
        var foreignKeys = (await connection.QueryAsync<ForeignKeyRow>(new CommandDefinition(ForeignKeysSql, parameters, cancellationToken: cancellationToken))).AsList();

        return Build(columns, primaryKeys, foreignKeys);
    }

    private static DatabaseSchema Build(List<ColumnRow> columns, List<PrimaryKeyRow> primaryKeys, List<ForeignKeyRow> foreignKeys)
    {
        var primaryKeyByTable = primaryKeys
            .GroupBy(row => (row.SchemaName, row.TableName))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)[.. group.Select(row => row.ColumnName)]);

        var foreignKeysByTable = foreignKeys
            .GroupBy(row => (row.SchemaName, row.TableName))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ForeignKeySchema>)[.. group
                    .GroupBy(row => row.ConstraintName)
                    .Select(fk => new ForeignKeySchema(
                        fk.Key,
                        [.. fk.Select(row => row.ColumnName)],
                        fk.First().ReferencedSchema,
                        fk.First().ReferencedTable,
                        [.. fk.Select(row => row.ReferencedColumn)]))]);

        var tables = columns
            .GroupBy(row => (row.SchemaName, row.TableName))
            .Select(table => new TableSchema(
                table.Key.SchemaName,
                table.Key.TableName,
                [.. table.Select(ToColumn)],
                primaryKeyByTable.GetValueOrDefault(table.Key, []),
                foreignKeysByTable.GetValueOrDefault(table.Key, [])));

        return new DatabaseSchema(tables);
    }

    private static ColumnSchema ToColumn(ColumnRow row) =>
        new(row.ColumnName, row.Ordinal, row.DataType, row.IsNullable, ToCharacterLength(row), row.Precision, row.Scale, row.IsIdentity, row.IsComputed);

    // sys.columns.max_length je u bajtovima (-1 = max); Unicode tipovi zauzimaju 2 bajta po karakteru.
    private static int? ToCharacterLength(ColumnRow row) => row switch
    {
        { MaxLengthBytes: < 0 } => null,
        { DataType: "nchar" or "nvarchar" } => row.MaxLengthBytes / 2,
        { DataType: "char" or "varchar" or "binary" or "varbinary" } => row.MaxLengthBytes,
        _ => null,
    };

    private sealed record ColumnRow(
        string SchemaName, string TableName, string ColumnName, int Ordinal, string DataType,
        bool IsNullable, short MaxLengthBytes, byte Precision, byte Scale, bool IsIdentity, bool IsComputed);

    private sealed record PrimaryKeyRow(string SchemaName, string TableName, string ColumnName);

    private sealed record ForeignKeyRow(
        string SchemaName, string TableName, string ConstraintName, string ColumnName,
        string ReferencedSchema, string ReferencedTable, string ReferencedColumn);
}
