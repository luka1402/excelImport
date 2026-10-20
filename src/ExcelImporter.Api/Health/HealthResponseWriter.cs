using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExcelImporter.Api.Health;

/// <summary>Piše sažet odgovor bez opisa i izuzetaka, jer /health može biti javno dostupan.</summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() }),
        };

        return JsonSerializer.SerializeAsync(context.Response.Body, payload, cancellationToken: context.RequestAborted);
    }
}