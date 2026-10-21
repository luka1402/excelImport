using ExcelImporter.Application.Rules;
using ExcelImporter.Domain.Rules;
using ExcelImporter.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>Lookup rečnik iz pravog SQL Servera (DemoDb.Gradovi): učitavanje, pogodak, whitelist i duplikat.</summary>
public sealed class SqlServerLookupDataSourceTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "DemoDb" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await ExecAsync(container.GetConnectionString(), await ReadInit("01-create-demodb.sql"));
        await ExecAsync(ConnectionString, await ReadInit("02-schema.sql"));
        await ExecAsync(ConnectionString, await ReadInit("03-seed.sql"));
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    private static LookupDictionaryLoader Loader()
    {
        var schemaProvider = new CachedSchemaProvider(new SqlServerSchemaReader(), Options.Create(new SchemaCacheOptions()), TimeProvider.System);
        return new LookupDictionaryLoader(schemaProvider, new SqlServerLookupDataSource());
    }

    private static LookupRule RuleFor(string table = "Gradovi", string key = "Naziv", string value = "Id") =>
        new() { TargetField = "GradId", SourceField = "Grad", Table = table, KeyColumn = key, ReturnColumn = value };

    [Fact]
    public async Task Hit_returns_database_id_ignoring_case_and_spaces()
    {
        var dictionary = await Loader().LoadAsync(ConnectionString, RuleFor(), TestContext.Current.CancellationToken);

        var row = new Dictionary<string, object?> { ["Grad"] = "  novi SAD " };
        var result = LookupEvaluator.Evaluate(RuleFor(), dictionary, row, 2, targetIsNullable: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(await ScalarAsync("SELECT Id FROM dbo.Gradovi WHERE Naziv = N'Novi Sad'"));
        dictionary.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Unknown_value_is_row_failure()
    {
        var dictionary = await Loader().LoadAsync(ConnectionString, RuleFor(), TestContext.Current.CancellationToken);

        var result = LookupEvaluator.Evaluate(RuleFor(), dictionary, new Dictionary<string, object?> { ["Grad"] = "Subotica" }, 5, true);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Nepostojeca", "Naziv", "Id")]
    [InlineData("Gradovi", "Nepostojeca", "Id")]
    [InlineData("Gradovi", "Naziv", "Nepostojeca")]
    public async Task Missing_table_or_column_is_rejected(string table, string key, string value) =>
        await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor(table, key, value), TestContext.Current.CancellationToken));

    [Fact]
    public async Task Duplicate_in_key_column_is_configuration_error()
    {
        // Drzava je na Gradovima duplirana (Srbija x3): ne sme da "prvi pobedi".
        var ex = await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor(key: "Drzava"), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Srbija");
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static Task<string> ReadInit(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "db", "init", file);
            if (File.Exists(candidate))
            {
                return File.ReadAllTextAsync(candidate, TestContext.Current.CancellationToken);
            }
        }

        throw new FileNotFoundException($"db/init/{file} nije pronađen.");
    }
}
