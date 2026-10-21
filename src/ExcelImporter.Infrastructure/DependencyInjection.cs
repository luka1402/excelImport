using ExcelImporter.Application.Files;
using ExcelImporter.Application.Schema;
using ExcelImporter.Infrastructure.Excel;
using ExcelImporter.Infrastructure.Persistence;
using ExcelImporter.Infrastructure.Schema;
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
        services.AddSingleton<ISchemaReader, SqlServerSchemaReader>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISchemaProvider, CachedSchemaProvider>();
        services.AddOptions<SchemaCacheOptions>();
        services.AddSingleton<IExcelSheetReader, ExcelDataSheetReader>();
        services.AddSingleton<IUploadedFileStore, LocalUploadedFileStore>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheckName, failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

        return services;
    }
}
