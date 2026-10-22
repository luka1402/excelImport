using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExcelImporter.Application.Schema;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public sealed class ConnectionEndpointTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly IConnectionCatalog catalog = Substitute.For<IConnectionCatalog>();
    private readonly HttpClient client;

    public ConnectionEndpointTests(ApiFactory factory)
    {
        client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddScoped(_ => catalog))).CreateClient();
    }

    public void Dispose() => client.Dispose();

    [Fact]
    public async Task List_ReturnsIdAndDisplayNameOnly()
    {
        catalog.ListAsync(Arg.Any<CancellationToken>()).Returns([new ConnectionSummary(1, "Produkcija")]);

        var response = await client.GetAsync("/api/connections", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body[0].GetProperty("id").GetInt32().ShouldBe(1);
        body[0].GetProperty("displayName").GetString().ShouldBe("Produkcija");
        body[0].EnumerateObject().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Schema_ReturnsTablesInCamelCase_WithoutConnectionString()
    {
        var table = new TableSchema(
            "dbo",
            "Customers",
            [new ColumnSchema("Id", 1, "int", false, null, 10, 0, true, false)],
            ["Id"],
            []);
        catalog.GetSchemaAsync(1, Arg.Any<CancellationToken>()).Returns(new DatabaseSchema([table]));

        var response = await client.GetAsync("/api/connections/1/schema", TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = JsonDocument.Parse(text).RootElement;

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tableJson = body.GetProperty("tables")[0];
        tableJson.GetProperty("schema").GetString().ShouldBe("dbo");
        tableJson.GetProperty("columns")[0].GetProperty("isIdentity").GetBoolean().ShouldBeTrue();
        tableJson.GetProperty("primaryKey")[0].GetString().ShouldBe("Id");
        text.ShouldNotContain(ApiFactory.SecretPassword);
    }

    [Fact]
    public async Task Schema_Returns404_WhenConnectionDoesNotExist()
    {
        catalog.GetSchemaAsync(99, Arg.Any<CancellationToken>()).Returns<DatabaseSchema>(_ => throw new ConnectionNotFoundException(99));

        var response = await client.GetAsync("/api/connections/99/schema", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Schema_Returns503WithMessage_WhenTargetDatabaseIsUnavailable()
    {
        catalog.GetSchemaAsync(1, Arg.Any<CancellationToken>()).Returns<DatabaseSchema>(
            _ => throw new ConnectionUnavailableException("Ciljna baza za konekciju 1 trenutno nije dostupna.", new InvalidOperationException(ApiFactory.ConnectionString)));

        var response = await client.GetAsync("/api/connections/1/schema", TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        text.ShouldContain("nije dostupna");
        text.ShouldNotContain(ApiFactory.SecretPassword);
    }
}
