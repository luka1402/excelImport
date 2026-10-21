using System.Diagnostics;
using ExcelImporter.Application.Rules;
using ExcelImporter.Domain.Rules;
using ExcelImporter.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

/// <summary>QA (COA-55): lookup pravilo — duplikati ključa, SQL tekst u imenima i vrednostima, veliki rečnik.</summary>
public sealed class QaLookupTests : IAsyncLifetime
{
    private const string Inject = "x]; DROP TABLE dbo.Gradovi; --";

    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "DemoDb" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await ExecAsync(container.GetConnectionString(), await ReadInit("01-create-demodb.sql"));
        await ExecAsync(ConnectionString, await ReadInit("02-schema.sql"));
        await ExecAsync(ConnectionString, await ReadInit("03-seed.sql"));
        await ExecAsync(ConnectionString, """
            CREATE TABLE dbo.QaKljuc (Id int NOT NULL, Naziv nvarchar(200) NULL, Povrat nvarchar(200) NULL);
            INSERT dbo.QaKljuc VALUES
              (1, N'DROP TABLE dbo.Gradovi; --', N'1'' OR 1=1 --'),
              (2, N'''; DELETE FROM dbo.Gradovi; --', N'2'),
              (3, N'Београд', N'Ćirilica'),
              (4, N'😀', N'emoji'),
              (5, N'x', NULL);
            CREATE TABLE dbo.QaDup (Id int NOT NULL, Naziv nvarchar(50) COLLATE Latin1_General_CS_AS NOT NULL);
            INSERT dbo.QaDup VALUES (1, N'Nis'), (2, N'nis');
            CREATE TABLE dbo.QaDupTrim (Id int NOT NULL, Naziv nvarchar(50) NOT NULL);
            INSERT dbo.QaDupTrim VALUES (1, N'Nis'), (2, N'Nis  ');
            CREATE TABLE dbo.QaBig (Id int NOT NULL, Naziv nvarchar(50) NOT NULL);
            INSERT dbo.QaBig SELECT TOP (200000) ROW_NUMBER() OVER (ORDER BY (SELECT 1)), CONCAT(N'K', ROW_NUMBER() OVER (ORDER BY (SELECT 1)))
              FROM sys.all_objects a CROSS JOIN sys.all_objects b;
            """);
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    private static LookupDictionaryLoader Loader() =>
        new(new CachedSchemaProvider(new SqlServerSchemaReader(), Options.Create(new SchemaCacheOptions()), TimeProvider.System),
            new SqlServerLookupDataSource());

    private static LookupRule RuleFor(string table, string key, string value, string schema = "dbo") =>
        new() { TargetField = "T", SourceField = "S", Schema = schema, Table = table, KeyColumn = key, ReturnColumn = value };

    [Theory]
    [InlineData("QaDup")]
    [InlineData("QaDupTrim")]
    public async Task Duplicate_keys_are_rejected_incl_case_and_trailing_spaces(string table)
    {
        var ex = await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor(table, "Naziv", "Id"), TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("Nis", Case.Insensitive);
    }

    [Theory]
    [InlineData(Inject, "Naziv", "Id")]
    [InlineData("Gradovi; DROP TABLE dbo.Gradovi", "Naziv", "Id")]
    [InlineData("QaKljuc", Inject, "Id")]
    [InlineData("QaKljuc", "Naziv", Inject)]
    [InlineData("QaKljuc", "Naziv]; DROP TABLE dbo.Gradovi; --", "Id")]
    public async Task Sql_text_as_table_or_column_name_is_rejected_and_nothing_is_executed(string table, string key, string value)
    {
        await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor(table, key, value), TestContext.Current.CancellationToken));

        (await ScalarAsync("SELECT COUNT(*) FROM dbo.Gradovi")).ShouldBe(4);
    }

    [Fact]
    public async Task Sql_text_as_schema_name_is_rejected()
    {
        await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor("Gradovi", "Naziv", "Id", "dbo; DROP TABLE dbo.Gradovi"), TestContext.Current.CancellationToken));
        (await ScalarAsync("SELECT COUNT(*) FROM dbo.Gradovi")).ShouldBe(4);
    }

    [Theory]
    [InlineData("DROP TABLE dbo.Gradovi; --", "1' OR 1=1 --")]
    [InlineData("'; DELETE FROM dbo.Gradovi; --", "2")]
    [InlineData("Београд", "Ćirilica")]
    [InlineData("😀", "emoji")]
    public async Task Sql_text_cyrillic_and_emoji_as_key_values_are_plain_data(string input, string expected)
    {
        var rule = RuleFor("QaKljuc", "Naziv", "Povrat");
        var dictionary = await Loader().LoadAsync(ConnectionString, rule, TestContext.Current.CancellationToken);

        var result = LookupEvaluator.Evaluate(rule, dictionary, new Dictionary<string, object?> { ["S"] = input }, 2, false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
        (await ScalarAsync("SELECT COUNT(*) FROM dbo.Gradovi")).ShouldBe(4);
    }

    [Fact]
    public async Task Sql_text_as_excel_value_that_is_not_in_dictionary_is_row_failure()
    {
        var rule = RuleFor("Gradovi", "Naziv", "Id");
        var dictionary = await Loader().LoadAsync(ConnectionString, rule, TestContext.Current.CancellationToken);

        var result = LookupEvaluator.Evaluate(rule, dictionary, new Dictionary<string, object?> { ["S"] = "'; DROP TABLE dbo.Gradovi; --" }, 3, true);

        result.IsSuccess.ShouldBeFalse();
        (await ScalarAsync("SELECT COUNT(*) FROM dbo.Gradovi")).ShouldBe(4);
    }

    [Fact]
    public async Task Null_return_value_is_row_failure()
    {
        var rule = RuleFor("QaKljuc", "Naziv", "Povrat");
        var dictionary = await Loader().LoadAsync(ConnectionString, rule, TestContext.Current.CancellationToken);

        LookupEvaluator.Evaluate(rule, dictionary, new Dictionary<string, object?> { ["S"] = "x" }, 4, true).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task Large_dictionary_loads_and_resolves_all_keys()
    {
        var rule = RuleFor("QaBig", "Naziv", "Id");
        var timer = Stopwatch.StartNew();
        var dictionary = await Loader().LoadAsync(ConnectionString, rule, TestContext.Current.CancellationToken);
        var loadTime = timer.Elapsed;

        dictionary.Count.ShouldBe(200_000);
        timer.Restart();
        for (var i = 1; i <= 200_000; i += 7)
        {
            var result = LookupEvaluator.Evaluate(rule, dictionary, new Dictionary<string, object?> { ["S"] = $" k{i} " }, i, false);
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldBe(i);
        }

        loadTime.ShouldBeLessThan(TimeSpan.FromSeconds(30));
        timer.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
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
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
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
