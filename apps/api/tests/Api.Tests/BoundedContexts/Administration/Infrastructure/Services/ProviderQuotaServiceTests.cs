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
}
