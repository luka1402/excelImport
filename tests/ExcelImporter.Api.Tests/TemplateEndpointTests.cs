using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExcelImporter.Application.Schema;
using ExcelImporter.Application.Templates;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public sealed class TemplateEndpointTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly string uploadDirectory = Path.Combine(Path.GetTempPath(), $"template-tests-{Guid.NewGuid():N}");
    private readonly HttpClient client;

    public TemplateEndpointTests(ApiFactory factory)
    {
        Directory.CreateDirectory(uploadDirectory);
        var store = new InMemoryStore();
        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("UploadStorage:Directory", uploadDirectory);
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ITemplateStore>(_ => store);
                services.AddScoped<ITemplateSchemaSource, FixedSchemaSource>();
            });
        }).CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        Directory.Delete(uploadDirectory, recursive: true);
    }

    private static object Body(string name, string targetColumn = "Name") => new
    {
        name,
        connectionId = 1,
        schemaName = "dbo",
        tableName = "Customers",
        sheetName = "List1",
        headerRow = 1,
        sourceColumns = new[] { "Ime", "NepostojecaKolona" },
        mappings = new[] { new { sourceColumn = "Ime", targetColumn } },
        rules = Array.Empty<object>(),
        constants = new[] { new { targetColumn = "Status", value = new { literal = "Aktivan" } } },
    };

    private async Task<JsonElement> CreateAsync(string name)
    {
        var response = await client.PostAsJsonAsync("/api/templates", Body(name), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateThenGet_ReturnsSavedState()
    {
        var created = await CreateAsync("Kupci");

        var response = await client.GetAsync($"/api/templates/{created.GetProperty("id").GetInt32()}", TestContext.Current.CancellationToken);
        var loaded = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        loaded.GetProperty("name").GetString().ShouldBe("Kupci");
        loaded.GetProperty("mappings")[0].GetProperty("targetColumn").GetString().ShouldBe("Name");
        loaded.GetProperty("constants")[0].GetProperty("value").GetProperty("literal").GetString().ShouldBe("Aktivan");
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        await CreateAsync("Duplikat");

        var response = await client.PostAsJsonAsync("/api/templates", Body("duplikat"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_UnknownTargetColumn_Returns400WithErrors()
    {
        var response = await client.PostAsJsonAsync("/api/templates", Body("Neispravan", "NemaTakve"), TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem.GetProperty("errors")[0].GetString()!.ShouldContain("NemaTakve");
    }

    [Fact]
    public async Task Create_UnknownRuleType_Returns400()
    {
        var body = JsonSerializer.SerializeToElement(Body("Pravila"));
        var withBadRule = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body.GetRawText())!;
        withBadRule["rules"] = JsonSerializer.SerializeToElement(new[] { new { type = "nepoznato", version = 1 } });

        var response = await client.PostAsJsonAsync("/api/templates", withBadRule, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingTemplate_Returns404()
    {
        (await client.GetAsync("/api/templates/9999", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync("/api/templates/9999", Body("X"), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync("/api/templates/9999", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesTemplate()
    {
        var created = await CreateAsync("Za brisanje");
        var id = created.GetProperty("id").GetInt32();

        (await client.DeleteAsync($"/api/templates/{id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/templates/{id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apply_FileWithDifferentColumns_ReturnsWarningWithLists()
    {
        var created = await CreateAsync("Primena");
        var fileId = Guid.NewGuid();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "samples/04-multi-sheet.xlsx"), Path.Combine(uploadDirectory, $"{fileId:N}.xlsx"));

        var response = await client.GetAsync($"/api/templates/{created.GetProperty("id").GetInt32()}/apply?fileId={fileId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("hasColumnWarning").GetBoolean().ShouldBeTrue();
        body.GetProperty("missingColumns").EnumerateArray().Select(e => e.GetString()).ShouldContain("NepostojecaKolona");
        body.GetProperty("isUsable").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Apply_UnknownFile_Returns404()
    {
        var created = await CreateAsync("Bez fajla");

        var response = await client.GetAsync($"/api/templates/{created.GetProperty("id").GetInt32()}/apply?fileId={Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class FixedSchemaSource : ITemplateSchemaSource
    {
        private static readonly DatabaseSchema Schema = new(
        [
            new TableSchema("dbo", "Customers",
            [
                new ColumnSchema("Id", 1, "int", false, null, 0, 0, true, false),
                new ColumnSchema("Name", 2, "nvarchar", true, 50, 0, 0, false, false),
                new ColumnSchema("Status", 3, "nvarchar", true, 10, 0, 0, false, false),
            ], ["Id"], []),
        ]);

        public Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken) => Task.FromResult(Schema);
    }

    private sealed class InMemoryStore : ITemplateStore
    {
        private readonly Lock gate = new();
        private readonly Dictionary<int, SavedTemplate> items = [];
        private int nextId = 1;

        public Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken)
        {
            lock (gate)
            {
                return Task.FromResult<IReadOnlyList<TemplateSummary>>(
                    [.. items.Values.Select(t => new TemplateSummary(t.Id, t.Name, t.ConnectionId, t.SchemaName, t.TableName, t.UpdatedAt))]);
            }
        }

        public Task<SavedTemplate?> FindAsync(int id, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                return Task.FromResult(items.GetValueOrDefault(id));
            }
        }

        public Task<SavedTemplate> AddAsync(TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (items.Values.Any(t => string.Equals(t.Name, draft.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DuplicateTemplateNameException(draft.Name);
                }

                var saved = new SavedTemplate(
                    nextId++, draft.Name, draft.ConnectionId, draft.SchemaName, draft.TableName, draft.SheetName, draft.HeaderRow,
                    columnsFingerprint, draft.Content, DateTime.UtcNow, DateTime.UtcNow);
                items[saved.Id] = saved;
                return Task.FromResult(saved);
            }
        }

        public Task<SavedTemplate?> UpdateAsync(int id, TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<SavedTemplate?>(null);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                return Task.FromResult(items.Remove(id));
            }
        }
    }
}