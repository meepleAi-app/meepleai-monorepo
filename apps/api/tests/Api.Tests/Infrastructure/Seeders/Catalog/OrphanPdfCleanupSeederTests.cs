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

    /// <summary>Seeds a legacy-scheme PDF (pre-migration <c>seed/…</c> FilePath) with no chunks.</summary>
    private static Guid SeedLegacyShellPdf(MeepleAiDbContext db, Guid? sharedGameId)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            FileName = "rulebook-legacy.pdf",
            FilePath = "seed/badsworm/legacy/rulebook.pdf",
            ContentHash = "hash",
            UploadedByUserId = Guid.NewGuid(),
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Failed),
        });
        return pdfId;
    }

    private static void SeedChunk(MeepleAiDbContext db, Guid pdfId)
    {
        db.TextChunks.Add(new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            Content = "chunk",
            ChunkIndex = 0,
            CharacterCount = 5,
        });
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

    // ---- legacy empty-shell selection (#3075) ----

    [Fact]
    public async Task FindLegacyShellPdfIds_LegacyPathZeroChunks_ReturnsShell()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var shellId = SeedLegacyShellPdf(db, gameId); // valid parent, seed/ path, no chunks
        await db.SaveChangesAsync();

        var shells = await OrphanPdfCleanupSeeder.FindLegacyShellPdfIdsAsync(db, CancellationToken.None);

        shells.Should().ContainSingle().Which.Should().Be(shellId);
    }

    [Fact]
    public async Task FindLegacyShellPdfIds_LegacyPathWithChunks_NotReturned()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var shellId = SeedLegacyShellPdf(db, gameId);
        SeedChunk(db, shellId); // still serving RAG → the 0-chunk guard must protect it
        await db.SaveChangesAsync();

        var shells = await OrphanPdfCleanupSeeder.FindLegacyShellPdfIdsAsync(db, CancellationToken.None);

        shells.Should().BeEmpty();
    }

    [Fact]
    public async Task FindLegacyShellPdfIds_ModernPathZeroChunks_NotReturned()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        SeedPdf(db, gameId); // modern pdfs/… path, 0 chunks → the path guard must protect it
        await db.SaveChangesAsync();

        var shells = await OrphanPdfCleanupSeeder.FindLegacyShellPdfIdsAsync(db, CancellationToken.None);

        shells.Should().BeEmpty();
    }

    // ---- cleanup delegation for legacy shells ----

    [Fact]
    public async Task CleanupAsync_SendsDeleteForLegacyShell_NotForModernSibling()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var shellId = SeedLegacyShellPdf(db, gameId);   // legacy shell → delete
        var modernSiblingId = SeedPdf(db, gameId);       // modern path, valid parent → keep
        await db.SaveChangesAsync();

        await OrphanPdfCleanupSeeder.CleanupAsync(db, _mediator.Object, _logger.Object, CancellationToken.None);

        _mediator.Verify(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == shellId), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == modernSiblingId), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupAsync_RowThatIsBothOrphanAndLegacyShell_DeletedExactlyOnce()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        // seed/ path + 0 chunks (legacy shell) AND SharedGameId pointing at a missing game (orphan).
        var bothId = SeedLegacyShellPdf(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        await OrphanPdfCleanupSeeder.CleanupAsync(db, _mediator.Object, _logger.Object, CancellationToken.None);

        // Union dedupe: appears in both id sets but is deleted only once.
        _mediator.Verify(m => m.Send(It.Is<DeleteKbDocumentCommand>(c => c.Id == bothId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
