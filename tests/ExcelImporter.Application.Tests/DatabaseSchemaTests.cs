using ExcelImporter.Application.Schema;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class DatabaseSchemaTests
{
    private static readonly DatabaseSchema Schema = new(
    [
        new TableSchema(
            "dbo", "Kupci",
            [new ColumnSchema("Id", 1, "int", false, null, 10, 0, true, false), new ColumnSchema("Ime", 2, "nvarchar", false, 100, 0, 0, false, false)],
            ["Id"], []),
    ]);

    [Theory]
    [InlineData("dbo", "Kupci", "Ime", true)]
    [InlineData("DBO", "kupci", "IME", true)]
    [InlineData("dbo", "Kupci", "Prezime", false)]
    [InlineData("dbo", "Nema", "Ime", false)]
    [InlineData("sys", "Kupci", "Ime", false)]
    [InlineData("dbo", "Kupci", "Ime; DROP TABLE Kupci", false)]
    [InlineData("dbo", "Kupci", "", false)]
    public void ColumnExists_checks_schema_table_and_column(string schema, string table, string column, bool expected) =>
        Schema.ColumnExists(schema, table, column).ShouldBe(expected);

    [Theory]
    [InlineData("dbo", "Kupci", true)]
    [InlineData("dbo", "Gradovi", false)]
    [InlineData("sys", "Kupci", false)]
    public void TableExists_checks_schema_and_table(string schema, string table, bool expected) =>
        Schema.TableExists(schema, table).ShouldBe(expected);

    [Fact]
    public async Task Provider_extensions_delegate_to_the_loaded_schema()
    {
        var provider = new FixedProvider(Schema);

        (await provider.ColumnExistsAsync("cs", "dbo", "Kupci", "Id", CancellationToken.None)).ShouldBeTrue();
        (await provider.ColumnExistsAsync("cs", "dbo", "Kupci", "X", CancellationToken.None)).ShouldBeFalse();
        (await provider.TableExistsAsync("cs", "dbo", "Kupci", CancellationToken.None)).ShouldBeTrue();
    }

    private sealed class FixedProvider(DatabaseSchema schema) : ISchemaProvider
    {
        public Task<DatabaseSchema> GetSchemaAsync(string connectionString, CancellationToken cancellationToken) => Task.FromResult(schema);

        public Task<DatabaseSchema> RefreshAsync(string connectionString, CancellationToken cancellationToken) => Task.FromResult(schema);
    }
}
