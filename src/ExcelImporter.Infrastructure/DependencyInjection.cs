using ExcelImporter.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExcelImporter.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Registruje infrastrukturne servise. <see cref="DatabaseOptions"/> registruje host (Api).</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProbe, SqlServerDatabaseProbe>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheckName, failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

        return services;
    }
}