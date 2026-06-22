using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Observability.Metrics;

[Trait("Category", "Unit")]
[Trait("Issue", "985")]
[Collection("ProviderQuotaMetricsRegistrar")]
public sealed class ProviderQuotaMetricsRegistrarTests
{
    private static ProviderQuotaDto BuildQuota(string name, decimal? used, decimal? remaining, decimal? limit = null)
        => new(
            ProviderName: name,
            QuotaSupported: true,
            TokenConfigured: true,
            UsedUsd: used,
            LimitUsd: limit,
            RemainingUsd: remaining,
            ResetAt: null,
            ErrorCode: null,
            ErrorMessage: null,
            FetchedAt: DateTime.UtcNow,
            CacheTtlSeconds: 300);

    [Fact]
    public void ObserveRemainingUsd_QueriesAllSupportedProviders()
    {
        var svc = new Mock<IProviderQuotaService>();
        svc.Setup(s => s.GetQuotaAsync("openrouter", It.IsAny<CancellationToken>()))
           .ReturnsAsync(BuildQuota("openrouter", 1.79m, null));
        svc.Setup(s => s.GetQuotaAsync("deepseek", It.IsAny<CancellationToken>()))
           .ReturnsAsync(BuildQuota("deepseek", null, 1.36m));

        ProviderQuotaMetricsRegistrar.Initialize(svc.Object, NullLogger.Instance);

        var measurements = ProviderQuotaMetricsRegistrar.ObserveRemainingUsd().ToList();
        var deepseek = measurements.FirstOrDefault(m =>
            m.Tags.ToArray().Any(t => t.Key == "provider" && (string?)t.Value == "deepseek"));

        deepseek.Value.Should().Be(1.36);
    }

    [Fact]
    public void ObserveUsedUsd_ReturnsUsedUsdWhenAvailable()
    {
        var svc = new Mock<IProviderQuotaService>();
        svc.Setup(s => s.GetQuotaAsync("openrouter", It.IsAny<CancellationToken>()))
           .ReturnsAsync(BuildQuota("openrouter", 1.79m, null));
        svc.Setup(s => s.GetQuotaAsync("deepseek", It.IsAny<CancellationToken>()))
           .ReturnsAsync(BuildQuota("deepseek", null, 1.36m));

        ProviderQuotaMetricsRegistrar.Initialize(svc.Object, NullLogger.Instance);

        var measurements = ProviderQuotaMetricsRegistrar.ObserveUsedUsd().ToList();
        var openrouter = measurements.FirstOrDefault(m =>
            m.Tags.ToArray().Any(t => t.Key == "provider" && (string?)t.Value == "openrouter"));

        openrouter.Value.Should().Be(1.79);
    }

    [Fact]
    public void ObserveLimitUsd_ReturnsLimitWhenAvailable()
    {
        var svc = new Mock<IProviderQuotaService>();
        svc.Setup(s => s.GetQuotaAsync("openrouter", It.IsAny<CancellationToken>()))
           .ReturnsAsync(BuildQuota("openrouter", 12m, 38m, limit: 50m));
        svc.Setup(s => s.GetQuotaAsync("deepseek", It.IsAny<CancellationToken>()))
           .ReturnsAsync(BuildQuota("deepseek", null, 1.36m, limit: null));

        ProviderQuotaMetricsRegistrar.Initialize(svc.Object, NullLogger.Instance);

        var measurements = ProviderQuotaMetricsRegistrar.ObserveLimitUsd().ToList();

        measurements.Should().HaveCount(1, because: "only OpenRouter has a limit; DeepSeek pay-as-you-go yields no measurement");
        measurements[0].Value.Should().Be(50);
        measurements[0].Tags.ToArray().Should().Contain(new KeyValuePair<string, object?>("provider", "openrouter"));
    }

    [Fact]
    public void ObserveRemainingUsd_SwallowsExceptions_ReturnsEmpty()
    {
        var svc = new Mock<IProviderQuotaService>();
        svc.Setup(s => s.GetQuotaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("upstream timeout"));

        ProviderQuotaMetricsRegistrar.Initialize(svc.Object, NullLogger.Instance);

        var result = ProviderQuotaMetricsRegistrar.ObserveRemainingUsd().ToList();

        result.Should().BeEmpty();
    }
}
