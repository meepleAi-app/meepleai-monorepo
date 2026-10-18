using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

/// <summary>
/// Come la valutazione conta un colpo (#4288): l'id del chunk di origine, al rango in cui il
/// recupero lo restituisce.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class EvaluationExecutorTests
{
    private static readonly Guid SourceChunk = Guid.NewGuid();

    [Fact]
    public async Task TheSourceChunkAtTheTop_IsAHitAtRankOne()
    {
        var outcome = await EvaluateAsync(SourceChunk, Guid.NewGuid());

        outcome.Metrics.Precision.At1.Should().Be(1);
        outcome.Metrics.Ranking.Mrr.Should().Be(1);
    }

    /// <summary>
    /// Un risultato senza id di chunk (un embedding scritto senza <c>source_chunk_id</c>) non è un
    /// colpo, ma occupa il suo posto: il chunk di origine dopo di lui resta secondo. Scartarlo lo
    /// porterebbe primo, e la valutazione sembrerebbe migliore di quella che è.
    /// </summary>
    [Fact]
    public async Task AResultWithoutAChunkId_IsNotAHit_AndKeepsItsRank()
    {
        var outcome = await EvaluateAsync(null, SourceChunk);

        outcome.Metrics.Precision.At1.Should().Be(0);
        outcome.Metrics.Ranking.Mrr.Should().Be(0.5);
    }

    private static Task<EvaluationOutcome> EvaluateAsync(params Guid?[] retrieved)
    {
        var sut = new EvaluationExecutor(
            new FixedSearch(retrieved),
            new EvaluationMetricsCalculator(),
            new AlwaysGreen());

        return sut.ExecuteAsync(
            Guid.NewGuid(),
            new PdfDocSnapshot(Guid.NewGuid(), "rulebook.pdf", 2, "Ready", []),
            [new GoldsetQaPair("q1", "How many cards?", "five", SourceChunk)],
            seed: 1,
            TestContext.Current.CancellationToken);
    }

    private sealed class AlwaysGreen : IQualityBandResolver
    {
        public QualityBand Resolve(EvaluationMetrics metrics) => QualityBand.Green;
    }

    private sealed class FixedSearch(IReadOnlyList<Guid?> ids) : IKbSearchProvider
    {
        public Task<SearchResult> SearchAsync(Guid docId, string question, int topK, CancellationToken ct) =>
            Task.FromResult(new SearchResult(ids, TimeSpan.Zero));
    }
}
