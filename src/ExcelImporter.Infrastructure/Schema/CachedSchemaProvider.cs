using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ExcelImporter.Application.Schema;
using Microsoft.Extensions.Options;

namespace ExcelImporter.Infrastructure.Schema;

public sealed class SchemaCacheOptions
{
    public const string SectionName = "SchemaCache";

    /// <summary>Koliko dugo se šema smatra svežom; posle toga se čita ponovo.</summary>
    public TimeSpan TimeToLive { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>Keš šeme po konekciji. Ključ je heš connection stringa, pa se tajna ne drži kao ključ u memoriji.</summary>
public sealed class CachedSchemaProvider(ISchemaReader reader, IOptions<SchemaCacheOptions> options, TimeProvider timeProvider) : ISchemaProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, Entry> entries = new();

    public void Dispose()
    {
        foreach (var entry in entries.Values)
        {
            entry.Dispose();
        }
    }

    public Task<DatabaseSchema> GetSchemaAsync(string connectionString, CancellationToken cancellationToken) =>
        EntryFor(connectionString).GetAsync(() => reader.ReadAsync(connectionString, cancellationToken), forceReload: false, Now, options.Value.TimeToLive, cancellationToken);

    public Task<DatabaseSchema> RefreshAsync(string connectionString, CancellationToken cancellationToken) =>
        EntryFor(connectionString).GetAsync(() => reader.ReadAsync(connectionString, cancellationToken), forceReload: true, Now, options.Value.TimeToLive, cancellationToken);

    private DateTimeOffset Now() => timeProvider.GetUtcNow();

    private Entry EntryFor(string connectionString) =>
        entries.GetOrAdd(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(connectionString))), _ => new Entry());

    private sealed class Entry : IDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private DatabaseSchema? schema;
        private DateTimeOffset expiresAt;
        private long version;

        public void Dispose() => gate.Dispose();

        public async Task<DatabaseSchema> GetAsync(
            Func<Task<DatabaseSchema>> load,
            bool forceReload,
            Func<DateTimeOffset> now,
            TimeSpan timeToLive,
            CancellationToken cancellationToken)
        {
            var versionSeen = Volatile.Read(ref version);
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Istovremeni pozivi dele jedno čitanje: ako je neko već osvežio šemu dok smo čekali, koristimo to.
                var refreshedWhileWaiting = version != versionSeen;
                var fresh = schema is not null && now() < expiresAt;
                if (fresh && (!forceReload || refreshedWhileWaiting))
                {
                    return schema!;
                }

                // Neuspešno čitanje se ne kešira: izuzetak izlazi pre izmene stanja.
                var loaded = await load();
                schema = loaded;
                expiresAt = now() + timeToLive;
                version++;
                return loaded;
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
