using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ExcelImporter.Infrastructure.Persistence;

internal sealed partial class DatabaseHealthCheck(IDatabaseProbe probe, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await probe.CheckConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Izuzetak se loguje (uz maskiranje), ali se ne vraća u rezultat jer se on šalje klijentu.
            LogProbeFailed(exception);
            return HealthCheckResult.Unhealthy("Baza nije dostupna.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Provera baze nije uspela.")]
    private partial void LogProbeFailed(Exception exception);
}
