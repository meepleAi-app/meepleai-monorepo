using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class QualityBandResolverTests
{
    private static QualityBandResolver Build(QualityBandsConfig bands)
    {
        var monitor = new TestOptionsMonitor<EvalQualityOptions>(
            new EvalQualityOptions { QualityBands = bands });
        return new QualityBandResolver(monitor);
    }

    private static QualityBandsConfig DefaultBands() => new()
    {
        PrecisionAt5 = new BandThreshold { RedMax = 0.40, YellowMax = 0.70 },
        Mrr = new BandThreshold { RedMax = 0.30, YellowMax = 0.60 },
        LatencyP95Ms = new BandThreshold { GreenMax = 30_000, YellowMax = 60_000, InvertedSeverity = true },
    };

    [Theory]
    [InlineData(0.39, QualityBand.Red)]
    [InlineData(0.40, QualityBand.Yellow)]
    [InlineData(0.69, QualityBand.Yellow)]
    [InlineData(0.70, QualityBand.Green)]
    [InlineData(0.95, QualityBand.Green)]
    public void Resolve_PrecisionAt5_AppliesRightExclusiveIntervals(double value, QualityBand expected)
    {
        var sut = Build(DefaultBands());
        var metrics = TestMetrics(precisionAt5: value, mrr: 1.0, latencyP95Ms: 0);

        sut.Resolve(metrics).Should().Be(expected);
    }

    [Fact]
    public void Resolve_LatencyAboveYellow_ReturnsRed()
    {
        var sut = Build(DefaultBands());
        var metrics = TestMetrics(precisionAt5: 1.0, mrr: 1.0, latencyP95Ms: 60_001);

        sut.Resolve(metrics).Should().Be(QualityBand.Red);
    }

    [Fact]
    public void Resolve_OverallBand_TakesMaxSeverity()
    {
        var sut = Build(DefaultBands());
        // precision green, mrr red, latency green → overall red
        var metrics = TestMetrics(precisionAt5: 0.9, mrr: 0.1, latencyP95Ms: 10);

        sut.Resolve(metrics).Should().Be(QualityBand.Red);
    }

    private static EvaluationMetrics TestMetrics(double precisionAt5, double mrr, double latencyP95Ms) => new(
        Precision: new PrecisionMetrics(0, 0, precisionAt5),
        Ranking: new RankingMetrics(mrr),
        Latency: new LatencyMetrics(TimeSpan.Zero, TimeSpan.FromMilliseconds(latencyP95Ms)),
        QueryCount: 10,
        CostUsd: 0.01m,
        QualityBand: QualityBand.Green);
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    where T : class
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
