using System.Diagnostics.Metrics;
using Api.Observability;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for the PDF cover-GENERATION metric (#3373 D5-C). Distinct from
/// <see cref="MeepleAiMetrics.CoverResolution"/> (read-time): this counter records
/// the outcome of each PDF cover generation attempt so ops can see generation
/// health without waiting for the lagging read-time placeholder&gt;80% signal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Observability")]
public class CatalogCoverMetricsTests
{
    private static List<(long Value, string? Source, string? Outcome)> Capture(Action act)
    {
        var captured = new List<(long, string?, string?)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.cover.generation.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? source = null;
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "source")
                {
                    source = tag.Value as string;
                }
                else if (tag.Key == "outcome")
                {
                    outcome = tag.Value as string;
                }
            }
            captured.Add((value, source, outcome));
        });
        listener.Start();

        act();

        return captured;
    }

    [Theory]
    [InlineData("generated")]
    [InlineData("skipped")]
    [InlineData("failed")]
    public void RecordPdfCoverGeneration_EmitsCounterOnceWithSourcePdfAndOutcomeTag(string outcome)
    {
        var captured = Capture(() => MeepleAiMetrics.RecordPdfCoverGeneration(outcome));

        // Tolerant: a concurrent test/scheduler may add other measurements on the
        // shared global counter — we only assert OUR increment made it through.
        captured.Should().Contain(m => m.Value == 1 && m.Source == "pdf" && m.Outcome == outcome);
    }

    [Fact]
    public void CoverGenerationOutcomeConstants_MatchLowercaseWireValues()
    {
        MeepleAiMetrics.CoverGenerationOutcomeGenerated.Should().Be("generated");
        MeepleAiMetrics.CoverGenerationOutcomeSkipped.Should().Be("skipped");
        MeepleAiMetrics.CoverGenerationOutcomeFailed.Should().Be("failed");
    }

    [Fact]
    public void CoverGeneration_Name_FollowsDottedConvention()
    {
        MeepleAiMetrics.CoverGeneration.Name.Should().Be("meepleai.cover.generation.total");
    }
}
