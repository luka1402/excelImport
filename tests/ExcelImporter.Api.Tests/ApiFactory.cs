using ExcelImporter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ExcelImporter.Api.Tests;

/// <summary>Host za testove: lažna provera baze, poznat connection string (tajna) i hvatanje svega što se loguje.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SecretPassword = "Sup3r-Secret-Pa55";
    public const string ConnectionString = $"Server=db.internal;Database=Importer;User Id=importer_user;Password={SecretPassword};";

    public IDatabaseProbe Probe { get; } = Substitute.For<IDatabaseProbe>();

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Probe);
            services.AddSingleton<ILoggerProvider>(Logs);
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, ThrowingEndpointStartupFilter>();
        });
    }
}

/// <summary>Dodaje /test/boom na kraj pipeline-a, iza exception handlera iz aplikacije.</summary>
internal sealed class ThrowingEndpointStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
{
    public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) =>
        app =>
        {
            next(app);
            app.Use((context, nextMiddleware) => context.Request.Path == "/test/boom"
                ? throw new InvalidOperationException($"Neuspelo povezivanje: {ApiFactory.ConnectionString}")
                : nextMiddleware(context));
        };
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> entries = new();

    public IReadOnlyCollection<string> Entries => entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(System.Collections.Concurrent.ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue($"{formatter(state, exception)} {exception}");
    }
}