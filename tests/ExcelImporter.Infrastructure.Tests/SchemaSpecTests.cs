using System.Text.RegularExpressions;
using ExcelImporter.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>QA (COA-22): sema ExcelImporterDb naspram O6 i ponovna primena migracija.</summary>
public sealed class SchemaSpecTests : IAsyncLifetime
{
    private const string InsertTemplate =
        "INSERT INTO ImportTemplate(Name,ConnectionId,SchemaName,TableName,HeaderRow,ColumnsFingerprint,MappingJson,RulesJson,ConstantsJson,CreatedAt,UpdatedAt) "
        + "VALUES (N't',1,N'dbo',N'T',1,N'f',N'{}',N'[]',N'{}',SYSUTCDATETIME(),SYSUTCDATETIME())";

    private static readonly string[] ExpectedColumns =
    [
        "ConnectionConfig.Id:int:0", "ConnectionConfig.Name:nvarchar:0", "ConnectionConfig.DisplayName:nvarchar:0", "ConnectionConfig.ConfigKey:nvarchar:0",
        "ImportTemplate.Id:int:0", "ImportTemplate.Name:nvarchar:0", "ImportTemplate.ConnectionId:int:0", "ImportTemplate.SchemaName:nvarchar:0",
        "ImportTemplate.TableName:nvarchar:0", "ImportTemplate.SheetName:nvarchar:1", "ImportTemplate.HeaderRow:int:0",
        "ImportTemplate.ColumnsFingerprint:nvarchar:0", "ImportTemplate.MappingJson:nvarchar:0", "ImportTemplate.RulesJson:nvarchar:0",
        "ImportTemplate.ConstantsJson:nvarchar:0", "ImportTemplate.CreatedAt:datetime2:0", "ImportTemplate.UpdatedAt:datetime2:0",
        "ImportJob.Id:bigint:0", "ImportJob.StartedAt:datetime2:0", "ImportJob.FinishedAt:datetime2:1", "ImportJob.FileName:nvarchar:0",
        "ImportJob.FileSize:bigint:0", "ImportJob.ConnectionId:int:0", "ImportJob.TargetTable:nvarchar:0", "ImportJob.TemplateId:int:1",
        "ImportJob.Mode:nvarchar:0", "ImportJob.Status:nvarchar:0", "ImportJob.TotalRows:int:0", "ImportJob.ImportedRows:int:0",
        "ImportJob.SkippedRows:int:0", "ImportJob.ErrorRows:int:0", "ImportJob.ErrorReportPath:nvarchar:1",
        "ImportError.Id:bigint:0", "ImportError.JobId:bigint:0", "ImportError.RowNumber:int:0", "ImportError.ColumnName:nvarchar:1", "ImportError.Reason:nvarchar:0",
    ];

    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "ExcelImporterDb" }.ConnectionString;

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Columns_match_O6_exactly()
    {
        await MigrateAsync();
        var actual = await QueryAsync(
            "SELECT TABLE_NAME + '.' + COLUMN_NAME + ':' + DATA_TYPE + ':' + CASE IS_NULLABLE WHEN 'YES' THEN '1' ELSE '0' END FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME <> '__EFMigrationsHistory'");
        actual.ShouldBe(ExpectedColumns, ignoreOrder: true);
    }

    [Fact]
    public async Task ImportJob_StartedAt_index_is_descending_and_only_name_indexes_are_unique()
    {
        await MigrateAsync();
        var desc = await QueryAsync("SELECT CAST(ic.is_descending_key AS varchar) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id WHERE i.name='IX_ImportJob_StartedAt'");
        desc.ShouldBe(["1"]);
        var unique = await QueryAsync("SELECT i.name FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id WHERE i.is_unique=1 AND t.is_ms_shipped=0 AND i.name LIKE 'IX[_]%' AND t.name IN ('ConnectionConfig','ImportTemplate','ImportJob','ImportError')");
        unique.ShouldBe(["IX_ConnectionConfig_Name", "IX_ImportTemplate_Name"], ignoreOrder: true);
    }

    [Fact]
    public async Task Foreign_keys_have_expected_delete_behavior()
    {
        await MigrateAsync();
        var fks = await QueryAsync("SELECT CAST(name AS nvarchar(200)) + N':' + CAST(delete_referential_action_desc AS nvarchar(50)) COLLATE DATABASE_DEFAULT FROM sys.foreign_keys");
        fks.ShouldBe(
        [
            "FK_ImportTemplate_ConnectionConfig_ConnectionId:NO_ACTION", "FK_ImportJob_ConnectionConfig_ConnectionId:NO_ACTION",
            "FK_ImportJob_ImportTemplate_TemplateId:SET_NULL", "FK_ImportError_ImportJob_JobId:CASCADE",
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Migrate_twice_is_noop_and_data_survives()
    {
        await MigrateAsync();
        await ExecAsync("INSERT INTO ConnectionConfig(Name,DisplayName,ConfigKey) VALUES (N'demo',N'Демо ћирилица 😀',N'ConnectionStrings__Demo')");
        await MigrateAsync();
        (await QueryAsync("SELECT DisplayName FROM ConnectionConfig")).ShouldBe(["Демо ћирилица 😀"]);
        (await QueryAsync("SELECT MigrationId FROM __EFMigrationsHistory")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Idempotent_db_init_script_applies_twice_and_matches_ef_migrate()
    {
        var script = await File.ReadAllTextAsync(FindRepoFile("db/init/10-excelimporterdb-migrations.sql"), TestContext.Current.CancellationToken);
        await ExecOnAsync(container.GetConnectionString(), "CREATE DATABASE ExcelImporterDb");
        await RunBatchesAsync(script);
        await ExecAsync("INSERT INTO ConnectionConfig(Name,DisplayName,ConfigKey) VALUES (N'a',N'A',N'K')");
        await RunBatchesAsync(script);
        (await QueryAsync("SELECT COUNT(*) FROM ConnectionConfig")).ShouldBe(["1"]);
        await using var ctx = new ExcelImporterDbContext(new DbContextOptionsBuilder<ExcelImporterDbContext>().UseSqlServer(ConnectionString).Options);
        (await ctx.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var actual = await QueryAsync(
            "SELECT TABLE_NAME + '.' + COLUMN_NAME + ':' + DATA_TYPE + ':' + CASE IS_NULLABLE WHEN 'YES' THEN '1' ELSE '0' END FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME <> '__EFMigrationsHistory'");
        actual.ShouldBe(ExpectedColumns, ignoreOrder: true);
    }

    [Fact]
    public async Task Constraints_behave()
    {
        await MigrateAsync();
        await ExecAsync("INSERT INTO ConnectionConfig(Name,DisplayName,ConfigKey) VALUES (N'a',N'A',N'K')");
        await Should.ThrowAsync<SqlException>(() => ExecAsync("INSERT INTO ConnectionConfig(Name,DisplayName,ConfigKey) VALUES (N'a',N'B',N'K2')"));
        await ExecAsync(InsertTemplate);
        await Should.ThrowAsync<SqlException>(() => ExecAsync(InsertTemplate));
        await Should.ThrowAsync<SqlException>(() => ExecAsync("DELETE FROM ConnectionConfig"));
        await ExecAsync("INSERT INTO ImportJob(StartedAt,FileName,FileSize,ConnectionId,TargetTable,TemplateId,Mode,Status,TotalRows,ImportedRows,SkippedRows,ErrorRows) VALUES (SYSUTCDATETIME(),N'f.xlsx',1,1,N'dbo.T',1,N'Append',N'Completed',1,1,0,0)");
        await ExecAsync("INSERT INTO ImportError(JobId,RowNumber,ColumnName,Reason) VALUES (1,2,NULL,N'x')");
        await ExecAsync("DELETE FROM ImportTemplate");
        (await QueryAsync("SELECT COUNT(*) FROM ImportJob WHERE TemplateId IS NULL")).ShouldBe(["1"]);
        await ExecAsync("DELETE FROM ImportJob");
        (await QueryAsync("SELECT COUNT(*) FROM ImportError")).ShouldBe(["0"]);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir.FullName, relative);
    }

    private static async Task ExecOnAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task MigrateAsync()
    {
        await using var ctx = new ExcelImporterDbContext(new DbContextOptionsBuilder<ExcelImporterDbContext>().UseSqlServer(ConnectionString).Options);
        await ctx.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    private async Task RunBatchesAsync(string script)
    {
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await ExecAsync(batch);
            }
        }
    }

    private Task ExecAsync(string sql) => ExecOnAsync(ConnectionString, sql);

    private async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var result = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            result.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return result;
    }
}


