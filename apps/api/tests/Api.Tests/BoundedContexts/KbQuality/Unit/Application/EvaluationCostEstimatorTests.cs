using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class EvaluationCostEstimatorTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task EstimateAsync_BasedOnChunkCount()
    {
        // 30 chunks → top-5 chunks goldset gen + 15 queries
        // = 5*$0.002 + 15*$0.001 = $0.025
        var pdf = new StubPdfReadModel(new PdfDocSnapshot(DocId, "test.pdf", ChunkCount: 30, "ready", []));
        var sut = new EvaluationCostEstimator(pdf);

        var cost = await sut.EstimateAsync(DocId, CancellationToken.None);

        cost.Should().BeApproximately(0.025m, 0.001m);
    }

    [Fact]
    public async Task EstimateAsync_MissingDoc_ReturnsZero()
    {
        var pdf = new StubPdfReadModel(snapshot: null);
        var sut = new EvaluationCostEstimator(pdf);

        var cost = await sut.EstimateAsync(DocId, CancellationToken.None);

        cost.Should().Be(0m);
    }

    [Fact]
    public async Task EstimateAsync_FewerThanFiveChunks_CapsAtAvailable()
    {
        // 3 chunks → top-3 chunks goldset gen + 9 queries
        // = 3*$0.002 + 9*$0.001 = $0.015
        var pdf = new StubPdfReadModel(new PdfDocSnapshot(DocId, "tiny.pdf", ChunkCount: 3, "ready", []));
        var sut = new EvaluationCostEstimator(pdf);

        var cost = await sut.EstimateAsync(DocId, CancellationToken.None);

        cost.Should().BeApproximately(0.015m, 0.001m);
    }

    private sealed class StubPdfReadModel(PdfDocSnapshot? snapshot) : IPdfDocumentReadModel
    {
        public Task<PdfDocSnapshot?> GetSnapshotAsync(Guid docId, CancellationToken ct)
            => Task.FromResult(snapshot);
    }
}
