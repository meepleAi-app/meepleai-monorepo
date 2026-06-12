using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for <see cref="PdfIndexingPipeline"/>.
/// Issue #2244 / epic #2242 Sub #2: single owner of the
/// "build VectorDocument + persist + raise event" flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfIndexingPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_NewDocument_CreatesAndPersistsViaRepository()
    {
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/dev/null",
            PrivateGameId = null,
            SharedGameId = sharedGameId,
            Language = "en",
            ExtractedText = new string('x', 1000)
        };

        var repo = new Mock<IVectorDocumentRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByGameAndSourceAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocument?)null);
        VectorDocument? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<VectorDocument>(), It.IsAny<CancellationToken>()))
            .Callback<VectorDocument, CancellationToken>((d, _) => captured = d)
            .Returns(Task.CompletedTask);

        var pipeline = new PdfIndexingPipeline(repo.Object, NullLogger<PdfIndexingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(pdfEntity, indexedChunkCount: 7, resolvedGameId: gameId, CancellationToken.None);

        result.Should().BeSameAs(captured);
        result.PdfDocumentId.Should().Be(pdfId);
        result.GameId.Should().Be(gameId);
        result.SharedGameId.Should().Be(sharedGameId);
        result.TotalChunks.Should().Be(7);
        result.TotalCharacters.Should().Be(1000, "pipeline must derive TotalCharacters from pdfDoc.ExtractedText.Length");
        result.DomainEvents.OfType<VectorDocumentIndexedEvent>().Should().HaveCount(1);
        repo.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDocument_UpdatesIdempotently()
    {
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/dev/null",
            SharedGameId = null,
            Language = "en"
        };

        var existing = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfId,
            language: "en",
            totalChunks: 3,
            indexedAt: DateTime.UtcNow.AddDays(-1),
            sharedGameId: null);

        var repo = new Mock<IVectorDocumentRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByGameAndSourceAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<VectorDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pipeline = new PdfIndexingPipeline(repo.Object, NullLogger<PdfIndexingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(pdfEntity, indexedChunkCount: 12, resolvedGameId: gameId, CancellationToken.None);

        result.Id.Should().Be(existing.Id, "re-index reuses the same aggregate id (idempotent)");
        result.TotalChunks.Should().Be(12, "re-index should reflect the new chunk count");
        repo.VerifyAll();
    }
}
