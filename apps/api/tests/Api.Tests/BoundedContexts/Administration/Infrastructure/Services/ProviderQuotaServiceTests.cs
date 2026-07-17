using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Providers.Quota;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "972")]
public sealed class ProviderQuotaServiceTests
{
    private sealed class PassThroughCache : IHybridCacheService
    {
        public Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            string[]? tags = null,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class
            => factory(ct);

        public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);
        public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(new HybridCacheStats());
    }

    private static ProviderQuotaService BuildSubject(
        IProviderQuotaProvider? provider,
        string providerName = "openrouter",
        Mock<IProviderCredentialResolver>? resolver = null)
    {
        var factory = new Mock<IProviderQuotaProviderFactory>();
        factory.Setup(f => f.GetProvider(providerName)).Returns(provider);
        factory.Setup(f => f.GetProvider(It.Is<string>(n => n != providerName))).Returns((IProviderQuotaProvider?)null);
        resolver ??= new Mock<IProviderCredentialResolver>();
        return new ProviderQuotaService(factory.Object, new PassThroughCache(), resolver.Object);
    }

    [Fact]
    public async Task GetQuotaAsync_UnknownProvider_ReturnsQuotaNotSupported()
    {
        var svc = BuildSubject(provider: null); // returns before the resolver is consulted

        var result = await svc.GetQuotaAsync("cohere", CancellationToken.None);

        result.QuotaSupported.Should().BeFalse();
        result.TokenConfigured.Should().BeFalse();
        result.ErrorCode.Should().Be("quota_not_supported");
        result.RemainingUsd.Should().BeNull();
    }

    [Fact]
    public async Task GetQuotaAsync_NotConfigured_ReturnsTokenConfiguredFalse()
    {
        var providerMock = new Mock<IProviderQuotaProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("openrouter");

        // #3044: neither a DB credential nor an env var → resolver throws → graceful not_configured.
        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCredentialNotConfiguredException("openrouter"));

        var svc = BuildSubject(providerMock.Object, resolver: resolver);

        var result = await svc.GetQuotaAsync("openrouter", CancellationToken.None);

        result.QuotaSupported.Should().BeTrue();
        result.TokenConfigured.Should().BeFalse();
        result.ErrorCode.Should().Be("not_configured");
        providerMock.Verify(p => p.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetQuotaAsync_HappyPath_ReturnsRemainingUsd()
    {
        var providerMock = new Mock<IProviderQuotaProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("openrouter");
        providerMock.Setup(p => p.FetchAsync("test-key-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaFetchResult(true, 12.5m, 50.0m, 37.5m, null, null, null));

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-key-secret");

        var svc = BuildSubject(providerMock.Object, resolver: resolver);

        var result = await svc.GetQuotaAsync("openrouter", CancellationToken.None);

        result.QuotaSupported.Should().BeTrue();
        result.TokenConfigured.Should().BeTrue();
        result.UsedUsd.Should().Be(12.5m);
        result.LimitUsd.Should().Be(50.0m);
        result.RemainingUsd.Should().Be(37.5m);
        result.CacheTtlSeconds.Should().Be(300);
    }

    [Fact]
    [Trait("Issue", "3044")]
    public async Task GetQuotaAsync_UsesResolvedKey_AfterRotation()
    {
        // Coerenza post-rotazione (#1859/#3044): il resolver ritorna la key ruotata (DB active-row),
        // che deve arrivare a FetchAsync — non una env-var stale letta con GetEnvironmentVariable.
        var providerMock = new Mock<IProviderQuotaProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("openrouter");
        providerMock.Setup(p => p.FetchAsync("rotated-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaFetchResult(true, 0m, 100m, 100m, null, null, null));

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync("rotated-key");

        var svc = BuildSubject(providerMock.Object, resolver: resolver);

        var result = await svc.GetQuotaAsync("openrouter", CancellationToken.None);

        result.RemainingUsd.Should().Be(100m);
        providerMock.Verify(p => p.FetchAsync("rotated-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Issue", "3043")]
    public async Task GetAllQuotasAsync_IteratesSupportedProviders_ReturnsOnePerName()
    {
        var orProvider = new Mock<IProviderQuotaProvider>();
        orProvider.SetupGet(p => p.ProviderName).Returns("openrouter");
        orProvider.Setup(p => p.FetchAsync("or-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaFetchResult(true, 10m, 40m, 30m, null, null, null));

        var dsProvider = new Mock<IProviderQuotaProvider>();
        dsProvider.SetupGet(p => p.ProviderName).Returns("deepseek");

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>())).ReturnsAsync("or-key");
        resolver.Setup(r => r.ResolveAsync("deepseek", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCredentialNotConfiguredException("deepseek")); // → not_configured

        var factory = new Mock<IProviderQuotaProviderFactory>();
        factory.SetupGet(f => f.SupportedProviderNames).Returns(new[] { "openrouter", "deepseek" });
        factory.Setup(f => f.GetProvider("openrouter")).Returns(orProvider.Object);
        factory.Setup(f => f.GetProvider("deepseek")).Returns(dsProvider.Object);
        var svc = new ProviderQuotaService(factory.Object, new PassThroughCache(), resolver.Object);

        var result = await svc.GetAllQuotasAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].ProviderName.Should().Be("openrouter");
        result[0].RemainingUsd.Should().Be(30m);          // happy path isolated
        result[1].ProviderName.Should().Be("deepseek");
        result[1].TokenConfigured.Should().BeFalse();      // not_configured isolated
        result[1].ErrorCode.Should().Be("not_configured");
    }

    [Fact]
    [Trait("Issue", "3043")]
    public async Task GetAllQuotasAsync_NoSupportedProviders_ReturnsEmpty()
    {
        var factory = new Mock<IProviderQuotaProviderFactory>();
        factory.SetupGet(f => f.SupportedProviderNames).Returns(Array.Empty<string>());
        var svc = new ProviderQuotaService(
            factory.Object, new PassThroughCache(), new Mock<IProviderCredentialResolver>().Object);

        var result = await svc.GetAllQuotasAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "3043")]
    public async Task GetAllQuotasAsync_OneProviderThrows_DegradesOnlyThatEntry()
    {
        var orProvider = new Mock<IProviderQuotaProvider>();
        orProvider.SetupGet(p => p.ProviderName).Returns("openrouter");
        orProvider.Setup(p => p.FetchAsync("or-key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom")); // e.g. malformed upstream body

        var dsProvider = new Mock<IProviderQuotaProvider>();
        dsProvider.SetupGet(p => p.ProviderName).Returns("deepseek");
        dsProvider.Setup(p => p.FetchAsync("ds-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaFetchResult(true, 1m, 10m, 9m, null, null, null));

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync("openrouter", It.IsAny<CancellationToken>())).ReturnsAsync("or-key");
        resolver.Setup(r => r.ResolveAsync("deepseek", It.IsAny<CancellationToken>())).ReturnsAsync("ds-key");

        var factory = new Mock<IProviderQuotaProviderFactory>();
        factory.SetupGet(f => f.SupportedProviderNames).Returns(new[] { "openrouter", "deepseek" });
        factory.Setup(f => f.GetProvider("openrouter")).Returns(orProvider.Object);
        factory.Setup(f => f.GetProvider("deepseek")).Returns(dsProvider.Object);
        var svc = new ProviderQuotaService(factory.Object, new PassThroughCache(), resolver.Object);

        var result = await svc.GetAllQuotasAsync(CancellationToken.None);

        // Per-provider isolation: one throwing provider degrades to fetch_error, it does NOT
        // fail the whole aggregate — the healthy provider is still returned.
        result.Should().HaveCount(2);
        result[0].ProviderName.Should().Be("openrouter");
        result[0].ErrorCode.Should().Be("fetch_error");
        result[1].ProviderName.Should().Be("deepseek");
        result[1].RemainingUsd.Should().Be(9m);
    }
}
