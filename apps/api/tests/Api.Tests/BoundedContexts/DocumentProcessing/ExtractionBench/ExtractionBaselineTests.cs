using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// #4434 (a) — il confronto con la baseline, provato da solo. È il pezzo che decide fra rosso e verde,
/// quindi ogni verso del confronto ha il suo test: un confronto che non sa dire «peggiorato» è un
/// banco che misura e non guarda.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ExtractionBaselineTests
{
    private static ExtractionBenchReport Report(params (string Fixture, string Metric, double? Value)[] cells) =>
        new("docnet", cells
            .GroupBy(c => c.Fixture, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new FixtureResult(
                    g.ToDictionary(c => c.Metric, c => c.Value, StringComparer.Ordinal),
                    Pages: 1,
                    Seconds: 0.1),
                StringComparer.Ordinal));

    [Fact]
    public void Regressions_IsEmpty_WhenTheRunEqualsTheBaseline()
    {
        var run = Report(("a", "m", 0.5), ("a", "n", null));

        ExtractionBaseline.FromReport(run).Regressions(run, tolerance: 0).Should().BeEmpty();
    }

    [Fact]
    public void Regressions_FlagsAValueBelowTheBaseline()
    {
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 1.0)));

        baseline.Regressions(Report(("a", "m", 0.75)), tolerance: 0)
            .Should().ContainSingle().Which.Should().Contain("a").And.Contain("m");
    }

    [Fact]
    public void Regressions_AcceptsAnImprovement_ItIsNotAFailure()
    {
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 0.5)));

        baseline.Regressions(Report(("a", "m", 1.0)), tolerance: 0).Should().BeEmpty();
    }

    [Fact]
    public void Regressions_HonoursTheToleranceAndNothingBeyondIt()
    {
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 0.9)));

        baseline.Regressions(Report(("a", "m", 0.85)), tolerance: 0.05).Should().BeEmpty();
        baseline.Regressions(Report(("a", "m", 0.80)), tolerance: 0.05).Should().HaveCount(1);
    }

    [Fact]
    public void Regressions_FlagsAMetricThatStoppedBeingApplicable()
    {
        // Era misurabile e ora non lo è più: la capacità è sparita, non è «un altro numero».
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 0.5)));

        baseline.Regressions(Report(("a", "m", null)), tolerance: 0).Should().HaveCount(1);
    }

    [Fact]
    public void Regressions_AcceptsAMetricThatBecameApplicable()
    {
        // Il verso opposto è una capacità nuova (per esempio le celle di #4439): non rompe nulla.
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", null)));

        baseline.Regressions(Report(("a", "m", 0.8)), tolerance: 0).Should().BeEmpty();
    }

    [Fact]
    public void Regressions_FlagsAFixtureMissingFromTheBaseline()
    {
        // Una fixture nuova senza baseline sarebbe non sorvegliata, in silenzio.
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 1.0)));

        baseline.Regressions(Report(("a", "m", 1.0), ("b", "m", 1.0)), tolerance: 0)
            .Should().ContainSingle().Which.Should().Contain("b").And.Contain("baseline");
    }

    [Fact]
    public void Regressions_FlagsAMetricMissingFromTheBaseline()
    {
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 1.0)));

        baseline.Regressions(Report(("a", "m", 1.0), ("a", "n", 1.0)), tolerance: 0)
            .Should().ContainSingle().Which.Should().Contain("n");
    }

    [Fact]
    public void Regressions_FlagsABaselineMetricThatTheRunDidNotProduce()
    {
        // Una metrica rinominata o tolta dal punteggio non deve sparire dal confronto in silenzio: la sua
        // riga nella baseline resterebbe a dichiarare un pavimento che nessuno controlla piu'.
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 1.0), ("a", "n", 1.0)));

        baseline.Regressions(Report(("a", "m", 1.0)), tolerance: 0)
            .Should().ContainSingle().Which.Should().Contain("n").And.Contain("did not produce");
    }

    [Fact]
    public void Regressions_FlagsABaselineFixtureThatTheRunDidNotMeasure()
    {
        // Se una fixture sparisce dal run (rinominata, cancellata), il confronto non può restare muto.
        var baseline = ExtractionBaseline.FromReport(Report(("a", "m", 1.0), ("b", "m", 1.0)));

        baseline.Regressions(Report(("a", "m", 1.0)), tolerance: 0)
            .Should().ContainSingle().Which.Should().Contain("b");
    }

    [Fact]
    public void Json_RoundTrips_PreservingNullsAndExactValues()
    {
        var original = ExtractionBaseline.FromReport(Report(("a", "m", 2.0 / 3.0), ("a", "n", null)));

        var restored = ExtractionBaseline.FromJson(original.ToJson());

        restored.Extractor.Should().Be("docnet");
        restored.Regressions(Report(("a", "m", 2.0 / 3.0), ("a", "n", null)), tolerance: 0).Should().BeEmpty();
        restored.Fixtures["a"]["n"].Should().BeNull();
    }
}
