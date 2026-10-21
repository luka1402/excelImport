using ExcelImporter.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>QA (COA-41): whitelist šeme prema sys.*, čudnim imenima i pogrešnim šemama, na pravom SQL Serveru.</summary>
public sealed class SchemaWhitelistQaTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
    private readonly SqlServerSchemaReader reader = new();

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "QaDb" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await ExecAsync(container.GetConnectionString(), "CREATE DATABASE QaDb");
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Weird_table_schema_and_column_names_are_read_verbatim_and_found()
    {
        await ExecAsync(ConnectionString, "CREATE SCHEMA [we ird]");
        await ExecAsync(ConnectionString, "CREATE SCHEMA [a]]b]");
        await ExecAsync(ConnectionString, "CREATE TABLE [we ird].[tab le] ([col umn] INT NOT NULL PRIMARY KEY, [it's] NVARCHAR(10) NULL, [x]]y] INT NULL)");
        await ExecAsync(ConnectionString, "CREATE TABLE [a]]b].[t'1] ([c;--] INT NULL)");
        await ExecAsync(ConnectionString, "CREATE TABLE dbo.[Таблица😀] ([Колона] INT NULL)");
        await ExecAsync(ConnectionString, "CREATE TABLE dbo.[Robert'); DROP TABLE Students;--] ([Id] INT NULL)");

        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        schema.ColumnExists("we ird", "tab le", "col umn").ShouldBeTrue();
        schema.ColumnExists("we ird", "tab le", "it's").ShouldBeTrue();
        schema.ColumnExists("we ird", "tab le", "x]y").ShouldBeTrue();
        schema.TableExists("a]b", "t'1").ShouldBeTrue();
        schema.ColumnExists("a]b", "t'1", "c;--").ShouldBeTrue();
        schema.TableExists("dbo", "Таблица😀").ShouldBeTrue();
        schema.ColumnExists("dbo", "Таблица😀", "Колона").ShouldBeTrue();
        schema.TableExists("dbo", "Robert'); DROP TABLE Students;--").ShouldBeTrue();

        // Neispravne varijante istih imena (bez escape-a, delimični nazivi, drugi sufiks) se ne smeju prepoznati.
        schema.TableExists("we ird", "[tab le]").ShouldBeFalse();
        schema.TableExists("we ird", "tab le ").ShouldBeFalse();
        schema.TableExists("we ird", "tab").ShouldBeFalse();
        schema.TableExists("we ird", "tab le]").ShouldBeFalse();
        schema.TableExists("a]]b", "t'1").ShouldBeFalse();
        schema.ColumnExists("we ird", "tab le", "x]]y").ShouldBeFalse();
    }

    [Theory]
    [InlineData("sys", "objects")]
    [InlineData("sys", "tables")]
    [InlineData("SYS", "sql_logins")]
    [InlineData("sys", "database_principals")]
    [InlineData("INFORMATION_SCHEMA", "TABLES")]
    [InlineData("information_schema", "columns")]
    [InlineData("", "Kupci")]
    [InlineData("dbo", "")]
    [InlineData("master.sys", "objects")]
    [InlineData("dbo.sys", "objects")]
    [InlineData("guest", "Kupci")]
    public async Task System_and_wrong_schema_lookups_are_rejected(string schemaName, string tableName)
    {
        await ExecAsync(ConnectionString, "CREATE TABLE dbo.Kupci (Id INT NOT NULL PRIMARY KEY)");

        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        schema.TableExists(schemaName, tableName).ShouldBeFalse();
        schema.ColumnExists(schemaName, tableName, "object_id").ShouldBeFalse();
        schema.Tables.ShouldAllBe(table => table.Schema != "sys" && table.Schema != "INFORMATION_SCHEMA");
    }

    [Fact]
    public async Task Exotic_column_types_do_not_break_reading()
    {
        await ExecAsync(ConnectionString,
            "CREATE TABLE dbo.Egzotika (Id INT NOT NULL PRIMARY KEY, G GEOGRAPHY NULL, H HIERARCHYID NULL, X XML NULL, R ROWVERSION, S SQL_VARIANT NULL, J JSON NULL, V VECTOR(3) NULL)");
        await ExecAsync(ConnectionString, "CREATE TYPE dbo.Telefon FROM VARCHAR(20) NOT NULL");
        await ExecAsync(ConnectionString, "CREATE TABLE dbo.Alias (Id INT NOT NULL, T dbo.Telefon)");

        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        schema.TableExists("dbo", "Egzotika").ShouldBeTrue();
        schema.FindTable("dbo", "Alias")!.FindColumn("T")!.DataType.ShouldBe("varchar");
    }

    [Fact]
    public async Task Views_and_system_versioned_history_do_not_leak_system_objects()
    {
        await ExecAsync(ConnectionString, "CREATE VIEW dbo.Pogled AS SELECT 1 AS A");
        await ExecAsync(ConnectionString, "CREATE TABLE dbo.Verz (Id INT NOT NULL PRIMARY KEY, SysStart DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL, SysEnd DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL, PERIOD FOR SYSTEM_TIME (SysStart, SysEnd)) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.VerzIstorija))");

        var schema = await reader.ReadAsync(ConnectionString, TestContext.Current.CancellationToken);

        schema.TableExists("dbo", "Pogled").ShouldBeFalse();
        schema.TableExists("dbo", "Verz").ShouldBeTrue();
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
