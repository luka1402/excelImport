using ExcelImporter.Application.Schema;
using ExcelImporter.Application.Templates;
using ExcelImporter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ExcelImporter.Infrastructure.Templates;

/// <summary>
/// Šema ciljne baze za konekciju šablona. Connection string se ne čuva u bazi: <c>ConnectionConfig.ConfigKey</c>
/// je ključ u sekciji <c>ConnectionStrings</c> (env varijabla ili user-secrets).
/// </summary>
internal sealed class ConfiguredTemplateSchemaSource(
    ExcelImporterDbContext context,
    IConfiguration configuration,
    ISchemaProvider schemaProvider) : ITemplateSchemaSource
{
    public async Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken)
    {
        var configKey = await context.Connections
            .AsNoTracking()
            .Where(connection => connection.Id == connectionId)
            .Select(connection => connection.ConfigKey)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new TemplateInvalidException([$"Konekcija {connectionId} ne postoji."]);

        var connectionString = configuration.GetConnectionString(configKey)
            ?? throw new TemplateInvalidException([$"Konekcija {connectionId} nije podešena (nedostaje vrednost za ključ \"{configKey}\")."]);

        return await schemaProvider.GetSchemaAsync(connectionString, cancellationToken);
    }
}