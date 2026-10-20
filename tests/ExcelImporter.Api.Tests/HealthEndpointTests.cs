using System.Net;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await factory.Probe.Received().CheckConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Health_ReturnsServiceUnavailable_WithoutLeakingConnectionString_WhenDatabaseFails()
    {
        factory.Probe.CheckConnectionAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException($"Ne mogu da se povežem: {ApiFactory.ConnectionString}"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        body.ShouldNotContain(ApiFactory.SecretPassword);
        body.ShouldNotContain("db.internal");
        factory.Probe.ClearSubstitute();
    }
}