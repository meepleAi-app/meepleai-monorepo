using Api.BoundedContexts.KbQuality.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class EvaluationMetricsCalculatorTests
{
    private readonly EvaluationMetricsCalculator _sut = new();

    [Fact]
    public void PrecisionAtK_AllRelevantInTopK_ReturnsOne()
    {
        var queryResults = new[]
        {
            new QueryResult(QueryId: "q1", RelevantHits: [true, true, true, false, false])
        };

        var metrics = _sut.Compute(queryResults);

        metrics.At1.Should().Be(1.0);
        metrics.At3.Should().Be(1.0);
        metrics.At5.Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void PrecisionAtK_AveragesAcrossQueries()
    {
        var queryResults = new[]
        {
            new QueryResult("q1", [true, false, false, false, false]),
            new QueryResult("q2", [false, false, false, false, false])
        };

        var metrics = _sut.Compute(queryResults);

        metrics.At1.Should().BeApproximately(0.5, 1e-9);
        metrics.At3.Should().BeApproximately((1.0 / 3 + 0) / 2, 1e-9);
        metrics.At5.Should().BeApproximately((0.2 + 0) / 2, 1e-9);
    }

    [Fact]
    public void Mrr_FirstRelevantAtRank1_ReturnsOne()
    {
        var queryResults = new[] { new QueryResult("q1", [true, false, false]) };

        var metrics = _sut.Compute(queryResults);

        metrics.Mrr.Should().Be(1.0);
    }

    [Fact]
    public void Mrr_AveragesReciprocalRanksAcrossQueries()
    {
        var queryResults = new[]
        {
            new QueryResult("q1", [false, true, false]),
            new QueryResult("q2", [false, false, false, true]),
            new QueryResult("q3", [false, false, false]),
        };

        var metrics = _sut.Compute(queryResults);

        metrics.Mrr.Should().BeApproximately((0.5 + 0.25 + 0) / 3.0, 1e-9);
    }

    [Fact]
    public void Compute_EmptyInput_ReturnsZeros()
    {
        var metrics = _sut.Compute(Array.Empty<QueryResult>());

        metrics.At1.Should().Be(0);
        metrics.At3.Should().Be(0);
        metrics.At5.Should().Be(0);
        metrics.Mrr.Should().Be(0);
    }
}
