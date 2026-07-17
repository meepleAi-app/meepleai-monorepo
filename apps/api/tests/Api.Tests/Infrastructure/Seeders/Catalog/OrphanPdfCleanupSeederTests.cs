using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders.Catalog;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

/// <summary>
/// Unit tests for #2907 — orphan pdf cleanup.
/// The selection logic (anti-join, honoring the SharedGames !IsDeleted global filter) is exercised
/// against in-memory EF; the delete cascade itself is delegated to DeleteKbDocumentCommand and
/// verified via a mocked IMediator here (real cascade is covered by an Integration test).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class OrphanPdfCleanupSeederTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILogger> _logger = new();

    public OrphanPdfCleanupSeederTests()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteKbDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Guid SeedGame(MeepleAiDbContext db, bool softDeleted = false)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Test Game", IsDeleted = softDeleted });
        return gameId;
    }

    private static Guid SeedPdf(MeepleAiDbContext db, Guid? sharedGameId)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            FileName = "rules.pdf",
            FilePath = $"pdfs/{pdfId}/file.pdf",
            ContentHash = "hash",
            UploadedByUserId = Guid.NewGuid(),
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
        });
        return pdfId;
    }

    // ---- selection logic ----

    [Fact]
    public async Task FindOrphanPdfIds_MissingParent_ReturnsOrphan()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var orphanPdfId = SeedPdf(db, Guid.NewGuid()); // SharedGameId points at a game that does not exist
        await db.SaveChangesAsync();

        var orphans = await OrphanPdfCleanupSeeder.FindOrphanPdfIdsAsync(db, CancellationToken.None);

        orphans.Should().ContainSingle().Which.Should().Be(orphanPdfId);
    }

    [Fact]
    public async Task FindOrphanPdfIds_SoftDeletedParent_ReturnsOrphan()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var softDeletedGameId = SeedGame(db, softDeleted: true);
        var orphanPdfId = SeedPdf(db, softDeletedGameId);
        await db.SaveChangesAsync();

        var orphans = await OrphanPdfCleanupSeeder.FindOrphanPdfIdsAsync(db, CancellationToken.None);

        orphans.Should().ContainSingle().Which.Should().Be(orphanPdfId);
    }

    [Fact]
    public async Task FindOrphanPdfIds_ValidParent_NotReturned()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        SeedPdf(db, gameId);
        await db.SaveChangesAsync();

        var orphans = await OrphanPdfCleanupSeeder.FindOrphanPdfIdsAsync(db, CancellationToken.None);

        orphans.Should().BeEmpty();
    }

    [Fact]
    public async Task FindOrphanPdfIds_NullSharedGameId_NotReturned()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        SeedPdf(db, sharedGameId: null); // dangling but not a shared-game orphan (out of #2907 scope)
        await db.SaveChangesAsync();

        var orphans = await OrphanPdfCleanupSeeder.FindOrphanPdfIdsAsync(db, CancellationToken.None);

        orphans.Should().BeEmpty();
    }

    // ---- cleanup delegation ----

    [Fact]
    public async Task CleanupAsync_SendsDeleteCommandForOrphansOnly()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var validGameId = SeedGame(db);
        var validPdfId = SeedPdf(db, validGameId);
        var orphanPdfId = SeedPdf(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        await OrphanPdfCleanupSeeder.CleanupAsync(db, _mediator.Object, _logger.Object, CancellationToken.None);

        _mediator.Verify(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == orphanPdfId), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == validPdfId), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupAsync_NoOrphans_SendsNothing()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var validGameId = SeedGame(db);
        SeedPdf(db, validGameId);
        await db.SaveChangesAsync();

        await OrphanPdfCleanupSeeder.CleanupAsync(db, _mediator.Object, _logger.Object, CancellationToken.None);

        _mediator.Verify(m => m.Send(It.IsAny<DeleteKbDocumentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupAsync_MediatorThrowsNotFound_SkipsAndContinues()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var orphanPdfId1 = SeedPdf(db, Guid.NewGuid());
        var orphanPdfId2 = SeedPdf(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        _mediator
            .Setup(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == orphanPdfId1), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("KbDocument", orphanPdfId1.ToString()));

        Func<Task> act = () => OrphanPdfCleanupSeeder.CleanupAsync(db, _mediator.Object, _logger.Object, CancellationToken.None);

        await act.Should().NotThrowAsync();
        // The failed orphan does not abort the batch: the second orphan is still processed.
        _mediator.Verify(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == orphanPdfId2), It.IsAny<CancellationToken>()), Times.Once);
    }
}
