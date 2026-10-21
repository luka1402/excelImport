using ExcelImporter.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>Čitanje šeme DemoDb (db/init skripte) iz pravog SQL Servera.</summary>
public sealed class SqlServerSchemaReaderTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
    private readonly SqlServerSchemaReader reader = new();

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "DemoDb" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await ExecAsync(container.GetConnectionString(), await File.ReadAllTextAsync(DbInitPath("01-create-demodb.sql"), TestContext.Current.CancellationToken));
        await ExecAsync(ConnectionString, await File.ReadAllTextAsync(DbInitPath("02-schema.sql"), TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Reads_DemoDb_tables_columns_types_nullability_and_lengths()
    {
        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        schema.Tables.Select(table => $"{table.Schema}.{table.Name}").ShouldBe(["dbo.Gradovi", "dbo.Kupci", "dbo.Proizvodi"]);

        var kupci = schema.FindTable("dbo", "Kupci")!;
        kupci.Columns.Select(column => column.Name).ShouldBe(["Id", "Ime", "Prezime", "Email", "GradId"]);
        var email = kupci.FindColumn("Email")!;
        email.DataType.ShouldBe("nvarchar");
        email.MaxLength.ShouldBe(200);
        email.IsNullable.ShouldBeFalse();
        kupci.FindColumn("Id")!.IsIdentity.ShouldBeTrue();
        kupci.FindColumn("Id")!.MaxLength.ShouldBeNull();

        var cena = schema.FindTable("dbo", "Proizvodi")!.FindColumn("Cena")!;
        (cena.DataType, cena.Precision, cena.Scale).ShouldBe(("decimal", (byte)18, (byte)2));
    }

    [Fact]
    public async Task Reads_primary_and_foreign_keys()
    {
        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        var kupci = schema.FindTable("dbo", "Kupci")!;
        kupci.PrimaryKey.ShouldBe(["Id"]);
        var fk = kupci.ForeignKeys.ShouldHaveSingleItem();
        fk.Name.ShouldBe("FK_Kupci_Gradovi");
        fk.Columns.ShouldBe(["GradId"]);
        (fk.ReferencedSchema, fk.ReferencedTable).ShouldBe(("dbo", "Gradovi"));
        fk.ReferencedColumns.ShouldBe(["Id"]);
        schema.FindTable("dbo", "Gradovi")!.ForeignKeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task System_schemas_and_tables_are_not_visible()
    {
        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        schema.Tables.ShouldNotContain(table => table.Schema == "sys" || table.Schema == "INFORMATION_SCHEMA");
        schema.TableExists("sys", "objects").ShouldBeFalse();
        schema.TableExists("INFORMATION_SCHEMA", "TABLES").ShouldBeFalse();
        schema.ColumnExists("dbo", "Kupci", "Ime").ShouldBeTrue();
        schema.ColumnExists("dbo", "Kupci", "Nepostojeca").ShouldBeFalse();
    }

    [Fact]
    public async Task Handles_composite_keys_max_types_other_schemas_and_cross_schema_foreign_keys()
    {
        await ExecAsync(ConnectionString, "CREATE SCHEMA hr");
        await ExecAsync(ConnectionString, "CREATE TABLE hr.Odeljenje (A INT NOT NULL, B INT NOT NULL, Opis NVARCHAR(MAX) NULL, Kod VARCHAR(20) NULL, Slika VARBINARY(MAX) NULL, CONSTRAINT PK_Odeljenje PRIMARY KEY (B, A))");
        await ExecAsync(ConnectionString, "CREATE TABLE dbo.Radnik (Id INT NOT NULL PRIMARY KEY, A INT NOT NULL, B INT NOT NULL, Ukupno AS (A + B), CONSTRAINT FK_Radnik_Odeljenje FOREIGN KEY (B, A) REFERENCES hr.Odeljenje (B, A))");

        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        var odeljenje = schema.FindTable("hr", "Odeljenje")!;
        odeljenje.PrimaryKey.ShouldBe(["B", "A"]);
        odeljenje.FindColumn("Opis")!.MaxLength.ShouldBeNull();
        odeljenje.FindColumn("Opis")!.IsNullable.ShouldBeTrue();
        odeljenje.FindColumn("Kod")!.MaxLength.ShouldBe(20);
        odeljenje.FindColumn("Slika")!.MaxLength.ShouldBeNull();

        var radnik = schema.FindTable("dbo", "Radnik")!;
        radnik.FindColumn("Ukupno")!.IsComputed.ShouldBeTrue();
        var fk = radnik.ForeignKeys.ShouldHaveSingleItem();
        fk.Columns.ShouldBe(["B", "A"]);
        (fk.ReferencedSchema, fk.ReferencedTable).ShouldBe(("hr", "Odeljenje"));
        fk.ReferencedColumns.ShouldBe(["B", "A"]);
    }

    [Fact]
    public async Task Provider_serves_cached_schema_until_refreshed()
    {
        var provider = new CachedSchemaProvider(reader, Microsoft.Extensions.Options.Options.Create(new SchemaCacheOptions()), TimeProvider.System);
        (await provider.GetSchemaAsync(ConnectionString, TestContext.Current.CancellationToken)).TableExists("dbo", "Novo").ShouldBeFalse();

        await ExecAsync(ConnectionString, "CREATE TABLE dbo.Novo (Id INT NOT NULL PRIMARY KEY)");

        (await provider.GetSchemaAsync(ConnectionString, TestContext.Current.CancellationToken)).TableExists("dbo", "Novo").ShouldBeFalse();
        (await provider.RefreshAsync(ConnectionString, TestContext.Current.CancellationToken)).TableExists("dbo", "Novo").ShouldBeTrue();
    }

    private static string DbInitPath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "db", "init", file);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"db/init/{file} nije pronađen iznad {AppContext.BaseDirectory}.");
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
