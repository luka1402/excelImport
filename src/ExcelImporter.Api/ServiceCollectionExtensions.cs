using ExcelImporter.Api.Configuration;
using ExcelImporter.Api.Errors;
using ExcelImporter.Api.Logging;
using ExcelImporter.Application.Files;
using ExcelImporter.Infrastructure;
using ExcelImporter.Infrastructure.Excel;
using ExcelImporter.Infrastructure.Persistence;
using FluentValidation;

namespace ExcelImporter.Api;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddApiFoundation(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        builder.Logging.ClearProviders();
        builder.Logging.AddConsoleFormatter<MaskingJsonConsoleFormatter, Microsoft.Extensions.Logging.Console.ConsoleFormatterOptions>();
        builder.Logging.AddConsole(options => options.FormatterName = MaskingJsonConsoleFormatter.FormatterName);
        services.AddSingleton<SecretMasker>();

        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<Configuration.CorsOptions>()
            .BindConfiguration(Configuration.CorsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ExcelReadingOptions>()
            .BindConfiguration(ExcelReadingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<UploadStorageOptions>().BindConfiguration(UploadStorageOptions.SectionName);
        services.AddOptions<OpenApiSettings>().BindConfiguration(OpenApiSettings.SectionName);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi();
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        services.AddInfrastructure();
        services.AddSingleton<FileAnalyzer>();

        var allowedOrigins = builder.Configuration.GetSection(Configuration.CorsOptions.SectionName).Get<Configuration.CorsOptions>()?.AllowedOrigins ?? [];
        services.AddCors(options => options.AddPolicy(Configuration.CorsOptions.PolicyName, policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

        return builder;
    }
}