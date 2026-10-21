using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Templates;
using ExcelImporter.Domain.Entities;
using ExcelImporter.Domain.Rules;
using ExcelImporter.Infrastructure.Persistence;
using ExcelImporter.Infrastructure.Templates;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.MsSql;

namespace ExcelImporter.Infrastructure.Tests;

public sealed class EfTemplateStoreTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
    private int connectionId;

    private string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = "ExcelImporterDb" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var connection = new ConnectionConfig { Name = "test", DisplayName = "Test", ConfigKey = "Test" };
        context.Connections.Add(connection);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        connectionId = connection.Id;
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    private ExcelImporterDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ExcelImporterDbContext>().UseSqlServer(ConnectionString).Options);

    private static EfTemplateStore CreateStore(ExcelImporterDbContext context) => new(context, TimeProvider.System);

    private TemplateDraft Draft(string name) => new(
        name, connectionId, "dbo", "Customers", "List1", 2,
        new TemplateContent(
            ["Ime", "Grad"],
            [new ColumnMapping("Ime", "Name")],
            [new DefaultValueRule { TargetField = "Status", Value = "Aktivan" }],
            [new ColumnConstant("Datum", ConstantValue.FromDynamic(DynamicConstantKind.Today))]));

    [Fact]
    public async Task Add_then_find_in_new_context_returns_identical_state()
    {
        int id;
        await using (var context = CreateContext())
        {
            id = (await CreateStore(context).AddAsync(Draft("Kupci"), "ABC", TestContext.Current.CancellationToken)).Id;
        }

        await using var other = CreateContext();
        var loaded = await CreateStore(other).FindAsync(id, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Kupci");
        loaded.HeaderRow.ShouldBe(2);
        loaded.ColumnsFingerprint.ShouldBe("ABC");
        var expected = Draft("Kupci").Content;
        loaded.Content.SourceColumns.ShouldBe(expected.SourceColumns);
        loaded.Content.Mappings.ShouldBe(expected.Mappings);
        loaded.Content.Rules.ShouldBe(expected.Rules);
        loaded.Content.Constants.ShouldBe(expected.Constants);
    }

    [Fact]
    public async Task Add_duplicate_name_is_rejected_case_insensitively()
    {
        await using var context = CreateContext();
        var store = CreateStore(context);
        await store.AddAsync(Draft("Kupci"), "A", TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DuplicateTemplateNameException>(
            () => store.AddAsync(Draft("KUPCI"), "A", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_unique_index_rejects_duplicate_name_even_when_precheck_is_bypassed()
    {
        await using (var context = CreateContext())
        {
            await CreateStore(context).AddAsync(Draft("Trka"), "A", TestContext.Current.CancellationToken);
        }

        // Dva konteksta koja su oba prošla proveru pre upisa: drugi upis mora pasti na indeksu, ne tiho uspeti.
        await using var racing = CreateContext();
        racing.Templates.Add(new ImportTemplate
        {
            Name = "Trka", ConnectionId = connectionId, SchemaName = "dbo", TableName = "T", ColumnsFingerprint = "A",
            MappingJson = "{}", RulesJson = "[]", ConstantsJson = "[]",
        });

        await Should.ThrowAsync<DbUpdateException>(() => racing.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_changes_content_and_keeps_creation_time_and_delete_removes()
    {
        await using var context = CreateContext();
        var store = CreateStore(context);
        var created = await store.AddAsync(Draft("Stari"), "A", TestContext.Current.CancellationToken);

        var updated = await store.UpdateAsync(created.Id, Draft("Novi"), "B", TestContext.Current.CancellationToken);

        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("Novi");
        updated.ColumnsFingerprint.ShouldBe("B");
        updated.CreatedAt.ShouldBe(created.CreatedAt);
        updated.UpdatedAt.ShouldBeGreaterThanOrEqualTo(created.UpdatedAt);

        (await store.ListAsync(TestContext.Current.CancellationToken)).Select(t => t.Name).ShouldBe(["Novi"]);
        (await store.DeleteAsync(created.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await store.DeleteAsync(created.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await store.UpdateAsync(created.Id, Draft("Novi"), "B", TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}