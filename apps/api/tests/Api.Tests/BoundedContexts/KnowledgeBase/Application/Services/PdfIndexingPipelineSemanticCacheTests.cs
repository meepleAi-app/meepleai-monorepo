using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Regression tests for #3982: indexing a PDF must invalidate the semantic response cache.
/// <para>
/// The invalidation used to live in <c>IndexPdfCommandHandler</c>, which is ONE of four callers of
/// <c>IPdfIndexingPipeline.IndexAsync</c>. The other three — <c>UploadPdfCommandHandler.Processing</c>,
/// <c>CompleteChunkedUploadCommandHandler</c> and <c>PdfProcessingPipelineService</c> — wrote a new
/// index and left the previous answers being served for the whole 24h TTL. Moving it into the
/// pipeline is what makes the omission not expressible: these tests pin that it happens here, so
/// a fifth caller inherits it for free.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3982")]
public sealed class PdfIndexingPipelineSemanticCacheTests
{
    private static (Mock<ISemanticResponseCache> Cache, PdfIndexingPipeline Pipeline) Build(Api.Infrastructure.MeepleAiDbContext db)
    {
        var cache = new Mock<ISemanticResponseCache>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pipeline = new PdfIndexingPipeline(
            db,
            mediator.Object,
            TimeProvider.System,
            cache.Object,
            NullLogger<PdfIndexingPipeline>.Instance);

        return (cache, pipeline);
    }

    private static VectorDocumentEntity Row(Guid pdfDocumentId, Guid gameId, string status) => new()
    {
        Id = Guid.NewGuid(),
        GameId = gameId,
        SharedGameId = null,
        PdfDocumentId = pdfDocumentId,
        ChunkCount = status == "completed" ? 7 : 0,
        TotalCharacters = status == "completed" ? 900 : 0,
        IndexingStatus = status,
        IndexedAt = DateTime.UtcNow.AddMinutes(-1),
    };

    /// <summary>
    /// Il caso base: una nuova indicizzazione invalida la cache del gioco.
    /// </summary>
    [Fact]
    public async Task IndexAsync_NewDocument_InvalidatesSemanticCacheForTheGame()
    {
        var pdfDocumentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (cache, pipeline) = Build(db);

        await pipeline.IndexAsync(
            pdfDocumentId: pdfDocumentId,
            gameId: gameId,
            sharedGameId: null,
            chunkCount: 5,
            totalCharacters: 1200,
            language: "it");

        cache.Verify(c => c.InvalidateGameAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Il percorso che il bail rendeva invisibile. Un documento già <c>completed</c> esce presto
    /// dalla pipeline per non ripubblicare l'evento di dominio — ma i chunk sono stati riscritti, e
    /// le risposte in cache sono stantie esattamente come al primo index. L'invalidazione sta quindi
    /// PRIMA di quel return.
    /// </summary>
    [Fact]
    public async Task IndexAsync_ReindexOfAlreadyCompletedDocument_StillInvalidates()
    {
        var pdfDocumentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.VectorDocuments.Add(Row(pdfDocumentId, gameId, "completed"));
        await db.SaveChangesAsync();

        var (cache, pipeline) = Build(db);

        await pipeline.IndexAsync(
            pdfDocumentId: pdfDocumentId,
            gameId: gameId,
            sharedGameId: null,
            chunkCount: 9,
            totalCharacters: 2400,
            language: "en");

        cache.Verify(c => c.InvalidateGameAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Nessun gioco, nessuna invalidazione: <c>AskQuestionQueryHandler</c> indicizza letture e
    /// scritture su <c>query.GameId</c>, che è sempre un gioco reale, quindi non esiste nulla in
    /// cache sotto un segnaposto da ripulire.
    /// </summary>
    [Fact]
    public async Task IndexAsync_WithoutGameId_DoesNotInvalidate()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (cache, pipeline) = Build(db);

        await pipeline.IndexAsync(
            pdfDocumentId: Guid.NewGuid(),
            gameId: null,
            sharedGameId: Guid.NewGuid(),
            chunkCount: 3,
            totalCharacters: 500,
            language: "en");

        cache.Verify(c => c.InvalidateGameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Best-effort, e deliberato: una cache che non si svuota non deve far fallire un indice già
    /// committato. Il costo di inghiottire è una risposta stantia per al massimo il TTL; il costo di
    /// rilanciare è un documento indicizzato nel database e riportato come fallito.
    /// </summary>
    [Fact]
    public async Task IndexAsync_WhenCacheThrows_IndexingStillSucceeds()
    {
        var pdfDocumentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (cache, pipeline) = Build(db);
        cache
            .Setup(c => c.InvalidateGameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis non raggiungibile"));

        var act = async () => await pipeline.IndexAsync(
            pdfDocumentId: pdfDocumentId,
            gameId: gameId,
            sharedGameId: null,
            chunkCount: 4,
            totalCharacters: 800,
            language: "en");

        await act.Should().NotThrowAsync();

        // E la riga è comunque stata scritta: il fallimento della cache non ha annullato l'indice.
        var row = db.VectorDocuments.Single(v => v.PdfDocumentId == pdfDocumentId);
        row.IndexingStatus.Should().Be("completed");
    }

    /// <summary>
    /// La cancellazione NON deve essere inghiottita: un annullamento è una richiesta del chiamante,
    /// non un guasto della cache, e trattarlo come best-effort maschererebbe uno shutdown.
    /// </summary>
    [Fact]
    public async Task IndexAsync_WhenCacheIsCancelled_PropagatesCancellation()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (cache, pipeline) = Build(db);
        cache
            .Setup(c => c.InvalidateGameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await pipeline.IndexAsync(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sharedGameId: null,
            chunkCount: 2,
            totalCharacters: 300,
            language: "en");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
