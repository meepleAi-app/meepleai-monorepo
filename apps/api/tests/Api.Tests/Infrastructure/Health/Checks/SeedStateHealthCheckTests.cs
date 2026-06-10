using Api.Infrastructure.Health.Checks;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Health.Checks;

/// <summary>
/// Unit tests for the pure state-machine inside <see cref="SeedStateHealthCheck"/>.
/// The DB-bound <c>CheckHealthAsync</c> path is exercised by integration tests
/// against the real DbContext — this class only locks down
/// <see cref="SeedStateHealthCheck.DeriveSeedState"/>.
///
/// Refs: #2126 D3 (seed_state field on /health/seed).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "2126")]
public sealed class SeedStateHealthCheckTests
{
    [Fact]
    public void DeriveSeedState_AllZero_ReturnsEmpty()
    {
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 0, pdfReady: 0, pdfFailed: 0, chunkCount: 0, embeddingCount: 0);
        state.Should().Be(SeedStateHealthCheck.SeedStates.Empty);
    }

    [Fact]
    public void DeriveSeedState_AllPdfsReady_CountsMatch_ReturnsReady()
    {
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 113, pdfReady: 113, pdfFailed: 0, chunkCount: 8599, embeddingCount: 8599);
        state.Should().Be(SeedStateHealthCheck.SeedStates.Ready);
    }

    [Fact]
    public void DeriveSeedState_SomePdfsFailed_ReturnsPartialFailed()
    {
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 113, pdfReady: 110, pdfFailed: 3, chunkCount: 8400, embeddingCount: 8400);
        state.Should().Be(SeedStateHealthCheck.SeedStates.PartialFailed);
    }

    [Fact]
    public void DeriveSeedState_ChunkEmbeddingMismatch_ReturnsPartialFailed()
    {
        // Sidecar invariant violated — same condition as D7 exit code 6 but
        // applied to the live DB rather than to a snapshot sidecar.
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 113, pdfReady: 113, pdfFailed: 0, chunkCount: 8599, embeddingCount: 8500);
        state.Should().Be(SeedStateHealthCheck.SeedStates.PartialFailed);
    }

    [Fact]
    public void DeriveSeedState_PdfsExistButNotAllReady_ReturnsIndexing()
    {
        // 8 PDFs uploaded, only 3 processed so far, no failures, counts agree.
        // Runtime indexing in flight.
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 8, pdfReady: 3, pdfFailed: 0, chunkCount: 240, embeddingCount: 240);
        state.Should().Be(SeedStateHealthCheck.SeedStates.Indexing);
    }

    [Fact]
    public void DeriveSeedState_ChunksWithoutEmbeddings_ReturnsPartialFailed()
    {
        // Embedding-service crashed mid-bake: chunking succeeded but embedding 0.
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 10, pdfReady: 10, pdfFailed: 0, chunkCount: 500, embeddingCount: 0);
        state.Should().Be(SeedStateHealthCheck.SeedStates.PartialFailed);
    }

    [Fact]
    public void DeriveSeedState_PdfsExistChunksZero_ReturnsIndexing()
    {
        // PDFs uploaded but chunking has not started yet.
        var state = SeedStateHealthCheck.DeriveSeedState(
            pdfTotal: 5, pdfReady: 0, pdfFailed: 0, chunkCount: 0, embeddingCount: 0);
        state.Should().Be(SeedStateHealthCheck.SeedStates.Indexing);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(113, 113, 0, 8599, 8599)]
    public void DeriveSeedState_ProducesStableEnumStrings(
        int pdfTotal, int pdfReady, int pdfFailed, int chunkCount, int embeddingCount)
    {
        // Lock down the enum strings so the public /health/seed contract is
        // not silently broken by a typo refactor.
        var state = SeedStateHealthCheck.DeriveSeedState(pdfTotal, pdfReady, pdfFailed, chunkCount, embeddingCount);
        new[]
        {
            SeedStateHealthCheck.SeedStates.Empty,
            SeedStateHealthCheck.SeedStates.Indexing,
            SeedStateHealthCheck.SeedStates.PartialFailed,
            SeedStateHealthCheck.SeedStates.Ready,
        }.Should().Contain(state);
    }

    [Fact]
    public void SeedStates_AreLowercaseSnakeCase()
    {
        // Public JSON contract: lowercase + snake_case so consumers don't have
        // to deal with ".NET enum case" inconsistencies.
        SeedStateHealthCheck.SeedStates.Empty.Should().Be("empty");
        SeedStateHealthCheck.SeedStates.Indexing.Should().Be("indexing");
        SeedStateHealthCheck.SeedStates.PartialFailed.Should().Be("partial_failed");
        SeedStateHealthCheck.SeedStates.Ready.Should().Be("ready");
    }
}
