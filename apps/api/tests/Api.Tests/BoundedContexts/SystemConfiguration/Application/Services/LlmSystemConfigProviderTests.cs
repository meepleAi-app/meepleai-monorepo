using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Tests.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Unit tests for LlmSystemConfigProvider (Issue #5498).
/// Verifies DB-first with appsettings fallback and caching behavior.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "5498")]
public sealed class LlmSystemConfigProviderTests
{
    private readonly Mock<ILlmSystemConfigRepository> _repoMock = new();
    private readonly IOptions<AiProviderSettings> _aiSettings;

    public LlmSystemConfigProviderTests()
    {
        _aiSettings = Options.Create(new AiProviderSettings
        {
            CircuitBreaker = new CircuitBreakerConfig
            {
                FailureThreshold = 7,
                OpenDurationSeconds = 45,
                SuccessThreshold = 4
            }
        });
    }

    private LlmSystemConfigProvider CreateProvider()
    {
        // Build a real IServiceScopeFactory that resolves our mock repo
        var services = new ServiceCollection();
        services.AddSingleton(_repoMock.Object);
        var sp = services.BuildServiceProvider();

        return new LlmSystemConfigProvider(
            sp.GetRequiredService<IServiceScopeFactory>(),
            _aiSettings,
            new LoggerFactory().CreateLogger<LlmSystemConfigProvider>());
    }

    [Fact]
    public async Task GetCircuitBreakerFailureThresholdAsync_WithDbConfig_ReturnsDbValue()
    {
        var config = LlmSystemConfig.CreateDefault();
        config.UpdateCircuitBreakerSettings(10, 60, 5);
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var provider = CreateProvider();
        var result = await provider.GetCircuitBreakerFailureThresholdAsync();

        result.Should().Be(10);
    }

    [Fact]
    public async Task GetCircuitBreakerFailureThresholdAsync_WithoutDbConfig_ReturnsFallback()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((LlmSystemConfig?)null);

        var provider = CreateProvider();
        var result = await provider.GetCircuitBreakerFailureThresholdAsync();

        result.Should().Be(7); // From AiProviderSettings
    }

    [Fact]
    public async Task GetDailyBudgetUsdAsync_WithDbConfig_ReturnsDbValue()
    {
        var config = LlmSystemConfig.CreateDefault();
        config.UpdateBudgetLimits(50.00m, 500.00m);
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var provider = CreateProvider();
        var result = await provider.GetDailyBudgetUsdAsync();

        result.Should().Be(50.00m);
    }

    [Fact]
    public async Task GetDailyBudgetUsdAsync_WithoutDbConfig_ReturnsHardcodedDefault()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((LlmSystemConfig?)null);

        var provider = CreateProvider();
        var result = await provider.GetDailyBudgetUsdAsync();

        result.Should().Be(10.00m); // Hardcoded fallback
    }

    [Fact]
    public async Task GetMonthlyBudgetUsdAsync_WithDbConfig_ReturnsDbValue()
    {
        var config = LlmSystemConfig.CreateDefault();
        config.UpdateBudgetLimits(50.00m, 500.00m);
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var provider = CreateProvider();
        var result = await provider.GetMonthlyBudgetUsdAsync();

        result.Should().Be(500.00m);
    }

    [Fact]
    public async Task CachesResultForSubsequentCalls()
    {
        var config = LlmSystemConfig.CreateDefault();
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var provider = CreateProvider();

        // Two calls should only hit DB once (cached)
        await provider.GetCircuitBreakerFailureThresholdAsync();
        await provider.GetCircuitBreakerFailureThresholdAsync();

        _repoMock.Verify(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCache_ForcesDbReload()
    {
        var config = LlmSystemConfig.CreateDefault();
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var provider = CreateProvider();

        await provider.GetCircuitBreakerFailureThresholdAsync();
        provider.InvalidateCache();
        await provider.GetCircuitBreakerFailureThresholdAsync();

        _repoMock.Verify(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DbFailure_ReturnsFallbackAndCachesNull()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var provider = CreateProvider();

        // Should return fallback value despite DB error
        var result = await provider.GetCircuitBreakerFailureThresholdAsync();
        result.Should().Be(7); // From AiProviderSettings

        // Second call should NOT hit DB again (null cached to prevent hammering)
        await provider.GetCircuitBreakerOpenDurationSecondsAsync();
        _repoMock.Verify(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetCircuitBreakerThresholdsSnapshot_CacheNotPopulated_ReturnsAppsettingsDefaults()
    {
        var provider = CreateProvider();

        var (failure, open, success) = provider.GetCircuitBreakerThresholdsSnapshot();

        failure.Should().Be(7); // From AiProviderSettings
        open.Should().Be(45);
        success.Should().Be(4);
    }

    [Fact]
    public async Task GetCircuitBreakerThresholdsSnapshot_CachePopulated_ReturnsCachedValues()
    {
        var config = LlmSystemConfig.CreateDefault();
        config.UpdateCircuitBreakerSettings(10, 60, 5);
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var provider = CreateProvider();

        // Populate cache via async call first
        await provider.GetCircuitBreakerFailureThresholdAsync();

        // Synchronous snapshot should now return DB values
        var (failure, open, success) = provider.GetCircuitBreakerThresholdsSnapshot();

        failure.Should().Be(10);
        open.Should().Be(60);
        success.Should().Be(5);
    }
}
