using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for the DI factory that selects between in-memory and Redis-backed
/// <see cref="IWikidataEnrichmentEventBroadcaster"/> based on the
/// <c>WIKIDATA_SSE_BACKPLANE</c> configuration key.
///
/// Issue #2256 — multi-pod Redis fan-out backplane for F4 SSE.
/// </summary>
/// <remarks>
/// These tests verify the env-var routing contract:
/// - default (unset)        → in-memory (backwards-compat for local dev).
/// - explicit "in-memory"   → in-memory.
/// - explicit "redis"       → Redis (requires <see cref="IConnectionMultiplexer"/>).
/// - invalid value          → throws fast at resolution time with a clear message.
/// The factory must be case-insensitive and trim whitespace, mirroring other env-var
/// routing in the codebase (e.g. <c>STORAGE_PROVIDER</c>).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2256")]
public class WikidataEnrichmentEventBroadcasterFactoryTests
{
    private static IServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        return services;
    }

    private static IConfiguration BuildConfig(string? backplane)
    {
        var settings = new Dictionary<string, string?>();
        if (backplane is not null)
        {
            settings["WIKIDATA_SSE_BACKPLANE"] = backplane;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public void Factory_BackplaneUnset_ResolvesInMemoryImplementation()
    {
        var services = BaseServices();
        services.AddWikidataEnrichmentEventBroadcaster(BuildConfig(backplane: null));

        using var sp = services.BuildServiceProvider();
        var broadcaster = sp.GetRequiredService<IWikidataEnrichmentEventBroadcaster>();

        broadcaster.Should().BeOfType<InMemoryWikidataEnrichmentEventBroadcaster>();
    }

    [Theory]
    [InlineData("in-memory")]
    [InlineData("IN-MEMORY")]
    [InlineData("  in-memory  ")]
    public void Factory_BackplaneInMemory_ResolvesInMemoryImplementation(string value)
    {
        var services = BaseServices();
        services.AddWikidataEnrichmentEventBroadcaster(BuildConfig(value));

        using var sp = services.BuildServiceProvider();
        var broadcaster = sp.GetRequiredService<IWikidataEnrichmentEventBroadcaster>();

        broadcaster.Should().BeOfType<InMemoryWikidataEnrichmentEventBroadcaster>();
    }

    [Theory]
    [InlineData("redis")]
    [InlineData("REDIS")]
    [InlineData("  Redis  ")]
    public void Factory_BackplaneRedis_ResolvesRedisImplementation(string value)
    {
        var services = BaseServices();
        // Stub multiplexer: the factory only reads it via the constructor;
        // GetSubscriber is not called until Publish or SubscribeAsync runs.
        var multiplexer = new Mock<IConnectionMultiplexer>(MockBehavior.Strict).Object;
        services.AddSingleton(multiplexer);
        services.AddWikidataEnrichmentEventBroadcaster(BuildConfig(value));

        using var sp = services.BuildServiceProvider();
        var broadcaster = sp.GetRequiredService<IWikidataEnrichmentEventBroadcaster>();

        broadcaster.Should().BeOfType<RedisWikidataEnrichmentEventBroadcaster>();
    }

    [Fact]
    public void Factory_BackplaneRedis_NoMultiplexerRegistered_ThrowsFastWithClearMessage()
    {
        var services = BaseServices();
        services.AddWikidataEnrichmentEventBroadcaster(BuildConfig("redis"));

        using var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IWikidataEnrichmentEventBroadcaster>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IConnectionMultiplexer*WIKIDATA_SSE_BACKPLANE=redis*");
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("memcached")]
    [InlineData("nats")]
    public void Factory_InvalidBackplaneValue_ThrowsFastWithClearMessage(string invalid)
    {
        var services = BaseServices();
        services.AddWikidataEnrichmentEventBroadcaster(BuildConfig(invalid));

        using var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IWikidataEnrichmentEventBroadcaster>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*WIKIDATA_SSE_BACKPLANE*'{invalid}'*in-memory*redis*");
    }
}
