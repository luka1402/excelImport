using ExcelImporter.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public class StartupConfigurationTests
{
    [Fact]
    public void Reporter_PrintsFailuresWithoutStackTrace_AndReturnsNonZero()
    {
        using var error = new StringWriter();
        var failure = new OptionsValidationException(
            nameof(DatabaseOptions), typeof(DatabaseOptions), ["Postavite Database__ConnectionString."]);

        var exitCode = StartupErrorReporter.Run(() => throw failure, error);

        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("Database__ConnectionString");
        error.ToString().ShouldNotContain(" at ");
    }

    [Fact]
    public void Reporter_ReturnsZero_WhenHostStopsNormally()
    {
        StartupErrorReporter.Run(() => { }, TextWriter.Null).ShouldBe(0);
    }
}



