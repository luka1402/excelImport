using System.Net;
using System.Text.Json;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public class ErrorHandlingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails_WithoutInternalDetails()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test/boom", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        document.RootElement.TryGetProperty("traceId", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("detail", out _).ShouldBeFalse();
        body.ShouldNotContain("InvalidOperationException");
        body.ShouldNotContain("   at ");
        body.ShouldNotContain(ApiFactory.SecretPassword);
        body.ShouldNotContain("db.internal");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/nepostojeca-ruta", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}