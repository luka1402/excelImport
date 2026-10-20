using System.Net;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public class OpenApiAndCorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task OpenApiDocument_IsAvailable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("openapi");
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOrigin_AndRejectsOthers()
    {
        using var client = factory.CreateClient();

        var allowed = new HttpRequestMessage(HttpMethod.Get, "/health");
        allowed.Headers.Add("Origin", "http://localhost:5173");
        var blocked = new HttpRequestMessage(HttpMethod.Get, "/health");
        blocked.Headers.Add("Origin", "http://evil.example");

        var allowedResponse = await client.SendAsync(allowed, TestContext.Current.CancellationToken);
        var blockedResponse = await client.SendAsync(blocked, TestContext.Current.CancellationToken);

        allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:5173"]);
        blockedResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}