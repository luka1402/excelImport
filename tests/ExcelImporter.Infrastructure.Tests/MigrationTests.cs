using ExcelImporter.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

public sealed class MigrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "ExcelImporterDb" }.ConnectionString;

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Migrations_apply_to_empty_database_with_all_tables_and_indexes()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        (await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();

        var tables = await QueryAsync("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME <> '__EFMigrationsHistory'");
        tables.ShouldBe(["ConnectionConfig", "ImportError", "ImportJob", "ImportTemplate"], ignoreOrder: true);

        var indexes = await QueryAsync("SELECT i.name FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.is_ms_shipped = 0 AND i.name LIKE 'IX[_]%'");
        indexes.ShouldBe(
        [
            "IX_ConnectionConfig_Name", "IX_ImportError_JobId", "IX_ImportJob_ConnectionId",
            "IX_ImportJob_StartedAt", "IX_ImportJob_TemplateId", "IX_ImportTemplate_ConnectionId", "IX_ImportTemplate_Name",
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Database_has_no_column_that_could_hold_a_connection_string()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var columns = await QueryAsync("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS");

        columns.ShouldNotContain(c => c.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
            || c.Contains("Password", StringComparison.OrdinalIgnoreCase));
        columns.ShouldContain("ConfigKey");
    }

    private ExcelImporterDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExcelImporterDbContext>().UseSqlServer(ConnectionString).Options);

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var result = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}


