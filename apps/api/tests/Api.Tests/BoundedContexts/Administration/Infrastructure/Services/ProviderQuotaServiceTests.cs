using Api.BoundedContexts.Administration.Infrastructure.Services;
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

    private static ProviderQuotaService BuildSubject(IProviderQuotaProvider? provider, string providerName = "openrouter")
    {
        var factory = new Mock<IProviderQuotaProviderFactory>();
        factory.Setup(f => f.GetProvider(providerName)).Returns(provider);
        factory.Setup(f => f.GetProvider(It.Is<string>(n => n != providerName))).Returns((IProviderQuotaProvider?)null);
        return new ProviderQuotaService(factory.Object, new PassThroughCache());
    }

    [Fact]
    public async Task GetQuotaAsync_UnknownProvider_ReturnsQuotaNotSupported()
    {
        var svc = BuildSubject(provider: null);

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
        providerMock.SetupGet(p => p.ApiKeyEnvVar).Returns("__ABSENT_QUOTA_VAR_972__");
        Environment.SetEnvironmentVariable("__ABSENT_QUOTA_VAR_972__", null);

        var svc = BuildSubject(providerMock.Object);

        var result = await svc.GetQuotaAsync("openrouter", CancellationToken.None);

        result.QuotaSupported.Should().BeTrue();
        result.TokenConfigured.Should().BeFalse();
        result.ErrorCode.Should().Be("not_configured");
        providerMock.Verify(p => p.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetQuotaAsync_HappyPath_ReturnsRemainingUsd()
    {
        const string envVar = "__OPENROUTER_QUOTA_TEST_972__";
        Environment.SetEnvironmentVariable(envVar, "test-key-secret");

        var providerMock = new Mock<IProviderQuotaProvider>();
        providerMock.SetupGet(p => p.ProviderName).Returns("openrouter");
        providerMock.SetupGet(p => p.ApiKeyEnvVar).Returns(envVar);
        providerMock.Setup(p => p.FetchAsync("test-key-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaFetchResult(true, 12.5m, 50.0m, 37.5m, null, null, null));

        try
        {
            var svc = BuildSubject(providerMock.Object);

            var result = await svc.GetQuotaAsync("openrouter", CancellationToken.None);

            result.QuotaSupported.Should().BeTrue();
            result.TokenConfigured.Should().BeTrue();
            result.UsedUsd.Should().Be(12.5m);
            result.LimitUsd.Should().Be(50.0m);
            result.RemainingUsd.Should().Be(37.5m);
            result.CacheTtlSeconds.Should().Be(300);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    [Trait("Issue", "3043")]
    public async Task GetAllQuotasAsync_IteratesSupportedProviders_ReturnsOnePerName()
    {
        const string orEnv = "__OR_QUOTA_ALL_3043__";
        Environment.SetEnvironmentVariable(orEnv, "or-key");
        Environment.SetEnvironmentVariable("__ABSENT_DS_3043__", null);
        try
        {
            var orProvider = new Mock<IProviderQuotaProvider>();
            orProvider.SetupGet(p => p.ProviderName).Returns("openrouter");
            orProvider.SetupGet(p => p.ApiKeyEnvVar).Returns(orEnv);
            orProvider.Setup(p => p.FetchAsync("or-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QuotaFetchResult(true, 10m, 40m, 30m, null, null, null));

            var dsProvider = new Mock<IProviderQuotaProvider>();
            dsProvider.SetupGet(p => p.ProviderName).Returns("deepseek");
            dsProvider.SetupGet(p => p.ApiKeyEnvVar).Returns("__ABSENT_DS_3043__"); // no key → not_configured

            var factory = new Mock<IProviderQuotaProviderFactory>();
            factory.SetupGet(f => f.SupportedProviderNames).Returns(new[] { "openrouter", "deepseek" });
            factory.Setup(f => f.GetProvider("openrouter")).Returns(orProvider.Object);
            factory.Setup(f => f.GetProvider("deepseek")).Returns(dsProvider.Object);
            var svc = new ProviderQuotaService(factory.Object, new PassThroughCache());

            var result = await svc.GetAllQuotasAsync(CancellationToken.None);

            result.Should().HaveCount(2);
            result[0].ProviderName.Should().Be("openrouter");
            result[0].RemainingUsd.Should().Be(30m);          // happy path isolated
            result[1].ProviderName.Should().Be("deepseek");
            result[1].TokenConfigured.Should().BeFalse();      // not_configured isolated
            result[1].ErrorCode.Should().Be("not_configured");
        }
        finally
        {
            Environment.SetEnvironmentVariable(orEnv, null);
        }
    }

    [Fact]
    [Trait("Issue", "3043")]
    public async Task GetAllQuotasAsync_NoSupportedProviders_ReturnsEmpty()
    {
        var factory = new Mock<IProviderQuotaProviderFactory>();
        factory.SetupGet(f => f.SupportedProviderNames).Returns(Array.Empty<string>());
        var svc = new ProviderQuotaService(factory.Object, new PassThroughCache());

        var result = await svc.GetAllQuotasAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "3043")]
    public async Task GetAllQuotasAsync_OneProviderThrows_DegradesOnlyThatEntry()
    {
        const string orEnv = "__OR_QUOTA_THROW_3043__";
        const string dsEnv = "__DS_QUOTA_OK_3043__";
        Environment.SetEnvironmentVariable(orEnv, "or-key");
        Environment.SetEnvironmentVariable(dsEnv, "ds-key");
        try
        {
            var orProvider = new Mock<IProviderQuotaProvider>();
            orProvider.SetupGet(p => p.ProviderName).Returns("openrouter");
            orProvider.SetupGet(p => p.ApiKeyEnvVar).Returns(orEnv);
            orProvider.Setup(p => p.FetchAsync("or-key", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom")); // e.g. malformed upstream body

            var dsProvider = new Mock<IProviderQuotaProvider>();
            dsProvider.SetupGet(p => p.ProviderName).Returns("deepseek");
            dsProvider.SetupGet(p => p.ApiKeyEnvVar).Returns(dsEnv);
            dsProvider.Setup(p => p.FetchAsync("ds-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QuotaFetchResult(true, 1m, 10m, 9m, null, null, null));

            var factory = new Mock<IProviderQuotaProviderFactory>();
            factory.SetupGet(f => f.SupportedProviderNames).Returns(new[] { "openrouter", "deepseek" });
            factory.Setup(f => f.GetProvider("openrouter")).Returns(orProvider.Object);
            factory.Setup(f => f.GetProvider("deepseek")).Returns(dsProvider.Object);
            var svc = new ProviderQuotaService(factory.Object, new PassThroughCache());

            var result = await svc.GetAllQuotasAsync(CancellationToken.None);

            // Per-provider isolation: one throwing provider degrades to fetch_error, it does NOT
            // fail the whole aggregate — the healthy provider is still returned.
            result.Should().HaveCount(2);
            result[0].ProviderName.Should().Be("openrouter");
            result[0].ErrorCode.Should().Be("fetch_error");
            result[1].ProviderName.Should().Be("deepseek");
            result[1].RemainingUsd.Should().Be(9m);
        }
        finally
        {
            Environment.SetEnvironmentVariable(orEnv, null);
            Environment.SetEnvironmentVariable(dsEnv, null);
        }
    }
}
