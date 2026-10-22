using ExcelImporter.Application.Files;
using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Application.Templates;
using ExcelImporter.Infrastructure.Excel;
using ExcelImporter.Infrastructure.Persistence;
using ExcelImporter.Infrastructure.Schema;
using ExcelImporter.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        services.AddSingleton<ILookupDataSource, SqlServerLookupDataSource>();
        services.AddSingleton<ILookupDictionaryLoader, LookupDictionaryLoader>();
        services.AddSingleton<IExcelSheetReader, ExcelDataSheetReader>();
        services.AddSingleton<IUploadedFileStore, LocalUploadedFileStore>();
        services.AddDbContext<ExcelImporterDbContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IConnectionCatalog, ConfiguredConnectionCatalog>();
        services.AddScoped<ITemplateStore, EfTemplateStore>();
        services.AddScoped<ITemplateSchemaSource, ConfiguredTemplateSchemaSource>();
        services.AddScoped<TemplateService>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheckName, failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

        return services;
    }
}
