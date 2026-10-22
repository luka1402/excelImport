using System.Data.Common;
using ExcelImporter.Application.Schema;
using ExcelImporter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExcelImporter.Infrastructure.Schema;

/// <summary>
/// Konekcije iz tabele <c>Connections</c>. Connection string se ne čuva u bazi: <c>ConnectionConfig.ConfigKey</c>
/// je ključ u sekciji <c>ConnectionStrings</c> (env varijabla ili user-secrets) i nikad ne napušta ovaj sloj.
/// </summary>
internal sealed partial class ConfiguredConnectionCatalog(
    ExcelImporterDbContext context,
    IConfiguration configuration,
    ISchemaProvider schemaProvider,
    ILogger<ConfiguredConnectionCatalog> logger) : IConnectionCatalog
{
    public async Task<IReadOnlyList<ConnectionSummary>> ListAsync(CancellationToken cancellationToken) =>
        await context.Connections
            .AsNoTracking()
            .OrderBy(connection => connection.DisplayName)
            .ThenBy(connection => connection.Id)
            .Select(connection => new ConnectionSummary(connection.Id, connection.DisplayName))
            .ToListAsync(cancellationToken);

    public async Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken)
    {
        var configKey = await context.Connections
            .AsNoTracking()
            .Where(connection => connection.Id == connectionId)
            .Select(connection => connection.ConfigKey)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ConnectionNotFoundException(connectionId);

        var connectionString = configuration.GetConnectionString(configKey);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogNotConfigured(connectionId, configKey);
            throw new ConnectionUnavailableException($"Konekcija {connectionId} nije podešena na serveru.");
        }

        try
        {
            return await schemaProvider.GetSchemaAsync(connectionString, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            // Izuzetak SQL klijenta može da sadrži naziv servera; klijentu ide samo opšta poruka.
            LogReadFailed(exception, connectionId);
            throw new ConnectionUnavailableException($"Ciljna baza za konekciju {connectionId} trenutno nije dostupna.", exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Konekcija {ConnectionId} nije podešena: nedostaje vrednost za ključ {ConfigKey}.")]
    private partial void LogNotConfigured(int connectionId, string configKey);

    [LoggerMessage(Level = LogLevel.Error, Message = "Čitanje šeme ciljne baze nije uspelo za konekciju {ConnectionId}.")]
    private partial void LogReadFailed(Exception exception, int connectionId);
}
