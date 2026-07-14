using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Regression test for the has_knowledge_base drift root cause: when a
/// VectorDocument row was created in "processing" state WITHOUT a SharedGameId,
/// the completed-transition previously published a VectorDocumentIndexedEvent
/// with a null SharedGameId, so VectorDocumentIndexedForKbFlagHandler skipped the
/// flag update. IndexAsync now heals the row's SharedGameId from the caller-supplied
/// value and publishes the event with it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PdfIndexingPipelineSharedGameIdHealTests
{
    [Fact]
    public async Task IndexAsync_CompletesProcessingRowWithNullSharedGameId_PublishesEventWithSharedGameIdAndHealsRow()
    {
        var pdfDocumentId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        // Pre-existing "processing" row created WITHOUT the SharedGameId link.
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            SharedGameId = null,
            PdfDocumentId = pdfDocumentId,
            ChunkCount = 0,
            TotalCharacters = 0,
            IndexingStatus = "processing",
            IndexedAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        VectorDocumentIndexedEvent? published = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<VectorDocumentIndexedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<VectorDocumentIndexedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var pipeline = new PdfIndexingPipeline(
            db,
            mediator.Object,
            TimeProvider.System,
            NullLogger<PdfIndexingPipeline>.Instance);

        await pipeline.IndexAsync(
            pdfDocumentId: pdfDocumentId,
            gameId: gameId,
            sharedGameId: sharedGameId,
            chunkCount: 12,
            totalCharacters: 3400,
            language: "en");

        // The event carries the SharedGameId the caller knew — not null.
        published.Should().NotBeNull();
        published!.SharedGameId.Should().Be(sharedGameId);

        // The row itself is healed so the link survives for downstream reads.
        var reloaded = await db.VectorDocuments.AsNoTracking()
            .SingleAsync(v => v.PdfDocumentId == pdfDocumentId);
        reloaded.SharedGameId.Should().Be(sharedGameId);
        reloaded.IndexingStatus.Should().Be("completed");
    }
}
