using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public sealed class QualityBandResolver(IOptionsMonitor<EvalQualityOptions> options) : IQualityBandResolver
{
    public QualityBand Resolve(EvaluationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        var bands = options.CurrentValue.QualityBands;

        var p5Band = Direct(metrics.Precision.At5, bands.PrecisionAt5);
        var mrrBand = Direct(metrics.Ranking.Mrr, bands.Mrr);
        var latencyBand = Inverted(metrics.Latency.P95.TotalMilliseconds, bands.LatencyP95Ms);

        var bandsList = new[] { p5Band, mrrBand, latencyBand };
        if (bandsList.Contains(QualityBand.Red)) return QualityBand.Red;
        if (bandsList.Contains(QualityBand.Yellow)) return QualityBand.Yellow;
        return QualityBand.Green;
    }

    private static QualityBand Direct(double value, BandThreshold t)
    {
        // Right-exclusive: red = [0, RedMax), yellow = [RedMax, YellowMax), green = [YellowMax, ∞)
        if (value < t.RedMax) return QualityBand.Red;
        if (value < t.YellowMax) return QualityBand.Yellow;
        return QualityBand.Green;
    }

    private static QualityBand Inverted(double value, BandThreshold t)
    {
        // Right-exclusive inverted: green = [0, GreenMax), yellow = [GreenMax, YellowMax), red = [YellowMax, ∞)
        if (value < t.GreenMax) return QualityBand.Green;
        if (value < t.YellowMax) return QualityBand.Yellow;
        return QualityBand.Red;
    }
}
