using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ExcelImporter.Infrastructure.Persistence;

internal sealed class SqlServerDatabaseProbe(IOptions<DatabaseOptions> options) : IDatabaseProbe
{
    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.HealthCheckTimeoutSeconds));

        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(timeout.Token);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(timeout.Token);
    }
}