using ExcelImporter.Api.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public class SecretMaskingTests
{
    private static SecretMasker CreateMasker() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = ApiFactory.ConnectionString })
            .Build());

    [Fact]
    public void Apply_MasksConfiguredConnectionString()
    {
        var result = CreateMasker().Apply($"Greška: {ApiFactory.ConnectionString} nije radila.");

        result.ShouldNotContain(ApiFactory.SecretPassword);
        result.ShouldContain(SecretMasker.Mask);
    }

    [Theory]
    [InlineData("Server=x;Password=abc123;Database=y")]
    [InlineData("server=x;PWD=abc123")]
    [InlineData("token=abc123 stiglo")]
    [InlineData("ApiKey: ok, api_key=abc123")]
    public void Apply_MasksSecretPairsEvenWhenNotConfigured(string text)
    {
        CreateMasker().Apply(text).ShouldNotContain("abc123");
    }

    [Fact]
    public void Apply_LeavesOrdinaryTextUntouched() =>
        CreateMasker().Apply("Uvoz završen: 120 redova").ShouldBe("Uvoz završen: 120 redova");

    [Fact]
    public async Task ConsoleOutput_NeverContainsConnectionString_WhenRequestFails()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        await client.GetAsync("/test/boom", TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        var writer = new StringWriter();
        var formatter = new MaskingJsonConsoleFormatter(CreateMasker());
        formatter.Write(
            new Microsoft.Extensions.Logging.Abstractions.LogEntry<string>(
                LogLevel.Error,
                "Test",
                new EventId(1),
                "poruka",
                new InvalidOperationException(ApiFactory.ConnectionString),
                (state, _) => $"{state} {ApiFactory.ConnectionString}"),
            scopeProvider: null,
            writer);

        writer.ToString().ShouldNotContain(ApiFactory.SecretPassword);
        writer.ToString().ShouldNotContain("importer_user");
        Console.Out.ShouldBe(originalOut);
    }
}