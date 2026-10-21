using System.Text;
using ExcelImporter.Application.Schema;
using ExcelImporter.Application.Templates;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ExcelImporter.Api.Tests;

/// <summary>QA (COA-73): neispravna tela zahteva za šablone ne smeju završiti sa 500.</summary>
public sealed class QaTemplateMalformedBodyTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public QaTemplateMalformedBodyTests(ApiFactory factory) =>
        client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddScoped<ITemplateStore, NullStore>();
            services.AddScoped<ITemplateSchemaSource, Schema>();
        })).CreateClient();

    public static TheoryData<string> Bodies => new()
    {
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[],"mappings":[null],"rules":[],"constants":[]}""",
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[null],"mappings":[],"rules":[],"constants":[]}""",
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[],"mappings":[],"rules":[null],"constants":[]}""",
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[],"mappings":[],"rules":[],"constants":[null]}""",
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[],"mappings":[],"rules":null,"constants":null}""",
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[],"mappings":[{"sourceColumn":"A"}],"rules":[],"constants":[]}""",
        """{"name":"A","connectionId":1,"schemaName":"dbo","tableName":"Customers","headerRow":1,"sourceColumns":[],"mappings":[],"rules":{},"constants":{}}""",
        """{"name":null,"connectionId":1,"schemaName":null,"tableName":null,"headerRow":1}""",
        """{}""",
        """null""",
    };

    [Theory]
    [MemberData(nameof(Bodies))]
    public async Task Create_MalformedBody_Returns4xx(string body)
    {
        var response = await client.PostAsync("/api/templates", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        ((int)response.StatusCode).ShouldBeInRange(400, 499, body);
    }

    private sealed class Schema : ITemplateSchemaSource
    {
        public Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken) =>
            Task.FromResult(new DatabaseSchema(
            [
                new TableSchema("dbo", "Customers",
                [
                    new ColumnSchema("Id", 1, "int", false, null, 0, 0, true, false),
                    new ColumnSchema("Name", 2, "nvarchar", true, 50, 0, 0, false, false),
                ], ["Id"], []),
            ]));
    }

    private sealed class NullStore : ITemplateStore
    {
        public Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TemplateSummary>>([]);

        public Task<SavedTemplate?> FindAsync(int id, CancellationToken cancellationToken) => Task.FromResult<SavedTemplate?>(null);

        public Task<SavedTemplate> AddAsync(TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken) =>
            Task.FromResult(new SavedTemplate(1, draft.Name, draft.ConnectionId, draft.SchemaName, draft.TableName, draft.SheetName, draft.HeaderRow, columnsFingerprint, draft.Content, DateTime.UtcNow, DateTime.UtcNow));

        public Task<SavedTemplate?> UpdateAsync(int id, TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken) => Task.FromResult<SavedTemplate?>(null);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}


