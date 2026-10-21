namespace ExcelImporter.Application.Schema;

/// <summary>Kolona ciljne tabele. <see cref="MaxLength"/> je u karakterima za (n)char/(n)varchar, u bajtovima za binary/varbinary, a null za (max) i ostale tipove.</summary>
public sealed record ColumnSchema(
    string Name,
    int Ordinal,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    byte Precision,
    byte Scale,
    bool IsIdentity,
    bool IsComputed);

public sealed record ForeignKeySchema(
    string Name,
    IReadOnlyList<string> Columns,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns);

public sealed record TableSchema(
    string Schema,
    string Name,
    IReadOnlyList<ColumnSchema> Columns,
    IReadOnlyList<string> PrimaryKey,
    IReadOnlyList<ForeignKeySchema> ForeignKeys)
{
    public ColumnSchema? FindColumn(string columnName) =>
        Columns.FirstOrDefault(column => string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Šema ciljne baze (samo korisničke tabele). Služi kao whitelist: svi nazivi koji dolaze od korisnika
/// moraju se proveriti ovde pre nego što uđu u SQL. Poređenje ne razlikuje velika i mala slova, kao podrazumevani SQL Server collation.
/// </summary>
public sealed class DatabaseSchema
{
    private readonly Dictionary<(string Schema, string Table), TableSchema> tablesByName;

    public DatabaseSchema(IEnumerable<TableSchema> tables)
    {
        Tables = [.. tables.OrderBy(table => table.Schema, StringComparer.OrdinalIgnoreCase).ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)];
        tablesByName = Tables.ToDictionary(table => (table.Schema, table.Name), NameComparer.Instance);
    }

    public IReadOnlyList<TableSchema> Tables { get; }

    public TableSchema? FindTable(string schemaName, string tableName) =>
        tablesByName.GetValueOrDefault((schemaName, tableName));

    public bool TableExists(string schemaName, string tableName) => FindTable(schemaName, tableName) is not null;

    /// <summary>Postoji li <c>šema.tabela.kolona</c> u ciljnoj bazi.</summary>
    public bool ColumnExists(string schemaName, string tableName, string columnName) =>
        FindTable(schemaName, tableName)?.FindColumn(columnName) is not null;

    private sealed class NameComparer : IEqualityComparer<(string Schema, string Table)>
    {
        public static readonly NameComparer Instance = new();

        public bool Equals((string Schema, string Table) x, (string Schema, string Table) y) =>
            string.Equals(x.Schema, y.Schema, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Table, y.Table, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Schema, string Table) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Schema),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Table));
    }
}
