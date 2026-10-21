using ExcelImporter.Api;
using ExcelImporter.Api.Configuration;
using ExcelImporter.Api.Files;
using ExcelImporter.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddApiFoundation();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsOptions.PolicyName);

if (app.Services.GetRequiredService<IOptions<OpenApiSettings>>().Value.Enabled)
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

app.MapFileEndpoints();

return StartupErrorReporter.Run(app.Run, Console.Error);

// Potrebno da WebApplicationFactory<Program> može da se koristi u Api.Tests.
public partial class Program;