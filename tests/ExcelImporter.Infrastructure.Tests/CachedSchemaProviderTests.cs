using ExcelImporter.Application.Schema;
using ExcelImporter.Infrastructure.Schema;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Infrastructure.Tests;

public class CachedSchemaProviderTests
{
    private static readonly DatabaseSchema Empty = new([]);
    private readonly ISchemaReader reader = Substitute.For<ISchemaReader>();
    private readonly ManualTime time = new();

    private CachedSchemaProvider CreateProvider() =>
        new(reader, Options.Create(new SchemaCacheOptions { TimeToLive = TimeSpan.FromMinutes(5) }), time);

    [Fact]
    public async Task Second_call_for_same_connection_uses_cache()
    {
        reader.ReadAsync("cs-a", Arg.Any<CancellationToken>()).Returns(Empty);
        var provider = CreateProvider();

        var first = await provider.GetSchemaAsync("cs-a", CancellationToken.None);
        var second = await provider.GetSchemaAsync("cs-a", CancellationToken.None);

        second.ShouldBeSameAs(first);
        await reader.Received(1).ReadAsync("cs-a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_connection_has_its_own_cache_entry()
    {
        reader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => new DatabaseSchema([]));
        var provider = CreateProvider();

        var a = await provider.GetSchemaAsync("cs-a", CancellationToken.None);
        var b = await provider.GetSchemaAsync("cs-b", CancellationToken.None);

        b.ShouldNotBeSameAs(a);
        await reader.Received(2).ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Expired_entry_is_reloaded()
    {
        reader.ReadAsync("cs", Arg.Any<CancellationToken>()).Returns(_ => new DatabaseSchema([]));
        var provider = CreateProvider();

        var first = await provider.GetSchemaAsync("cs", CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(6));
        var second = await provider.GetSchemaAsync("cs", CancellationToken.None);

        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task Refresh_reloads_even_when_entry_is_fresh()
    {
        reader.ReadAsync("cs", Arg.Any<CancellationToken>()).Returns(_ => new DatabaseSchema([]));
        var provider = CreateProvider();

        var first = await provider.GetSchemaAsync("cs", CancellationToken.None);
        var refreshed = await provider.RefreshAsync("cs", CancellationToken.None);
        var afterRefresh = await provider.GetSchemaAsync("cs", CancellationToken.None);

        refreshed.ShouldNotBeSameAs(first);
        afterRefresh.ShouldBeSameAs(refreshed);
    }

    [Fact]
    public async Task Failed_read_is_not_cached()
    {
        reader.ReadAsync("cs", Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException<DatabaseSchema>(new InvalidOperationException("down")),
            _ => Task.FromResult(Empty));
        var provider = CreateProvider();

        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetSchemaAsync("cs", CancellationToken.None));
        (await provider.GetSchemaAsync("cs", CancellationToken.None)).ShouldBeSameAs(Empty);
    }

    [Fact]
    public async Task Concurrent_calls_share_a_single_read()
    {
        var release = new TaskCompletionSource<DatabaseSchema>();
        reader.ReadAsync("cs", Arg.Any<CancellationToken>()).Returns(release.Task);
        var provider = CreateProvider();

        var calls = Enumerable.Range(0, 5).Select(_ => provider.GetSchemaAsync("cs", CancellationToken.None)).ToArray();
        release.SetResult(Empty);
        var results = await Task.WhenAll(calls);

        results.ShouldAllBe(schema => ReferenceEquals(schema, Empty));
        await reader.Received(1).ReadAsync("cs", Arg.Any<CancellationToken>());
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
