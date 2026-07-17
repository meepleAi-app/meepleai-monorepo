using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders.Catalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for the #2907 (orphan cleanup) + #2908 (re-enqueue stale Pending) seed passes.
/// Exercises the real Npgsql anti-join (honoring the SharedGames !IsDeleted global filter), the real
/// EF cascade delete of text_chunks + vector_document, and the interaction (cleanup before re-enqueue).
/// pgvector embeddings deletion is delegated to a mocked IVectorStoreAdapter (no real embeddings table),
/// mirroring DeleteKbDocumentCommandHandlerIntegrationTests.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "2907")]
public sealed class OrphanCleanupAndReattemptIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid SystemUserId = new("A0000000-0000-0000-0000-0000000029A7");
    private static readonly Guid ValidGameId = new("B0000000-0000-0000-0000-0000000029A7");
    private static readonly Guid SoftDeletedGameId = new("B0000000-0000-0000-0000-0000000029A8");

    public OrphanCleanupAndReattemptIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_orphancleanup_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        var blobMock = new Mock<IBlobStorageService>();
        blobMock.Setup(b => b.DeleteAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobMock.Object);

        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock.Setup(v => v.DeleteByVectorDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddScoped<IVectorStoreAdapter>(_ => vectorStoreMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await TestMigrationHelper.MigrateWithRetryAsync(_dbContext, Ct);
        await SeedBaseEntitiesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task CleanupThenReattempt_RemovesOrphansCascade_AndReEnqueuesOnlyValidPending()
    {
        // Arrange
        var orphanReadyPdfId = await SeedPdfWithChildrenAsync(SoftDeletedGameId, "Ready");
        var orphanPendingPdfId = await SeedPdfWithChildrenAsync(SoftDeletedGameId, "Pending");
        var validPendingPdfId = await SeedPdfWithChildrenAsync(ValidGameId, "Pending");
        var validReadyPdfId = await SeedPdfWithChildrenAsync(ValidGameId, "Ready");

        // Act — the CatalogSeedLayer order: cleanup (#2907) then re-enqueue (#2908)
        await OrphanPdfCleanupSeeder.CleanupAsync(_dbContext!, _mediator!, NullLogger.Instance, Ct);
        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(_dbContext!, SystemUserId, NullLogger.Instance, Ct);

        // Assert — orphans (soft-deleted parent) removed with a real EF cascade
        (await _dbContext!.PdfDocuments.FindAsync(new object[] { orphanReadyPdfId }, Ct))
            .Should().BeNull("orphan PDF must be hard-deleted");
        (await _dbContext.PdfDocuments.FindAsync(new object[] { orphanPendingPdfId }, Ct))
            .Should().BeNull("orphan Pending PDF must be hard-deleted, not re-enqueued");
        (await _dbContext.TextChunks.CountAsync(c => c.PdfDocumentId == orphanReadyPdfId, Ct))
            .Should().Be(0, "text chunks cascade-delete with the orphan PDF");
        (await _dbContext.VectorDocuments.CountAsync(v => v.PdfDocumentId == orphanReadyPdfId, Ct))
            .Should().Be(0, "vector document cascade-deletes with the orphan PDF");

        // Assert — valid PDFs untouched by cleanup
        (await _dbContext.PdfDocuments.FindAsync(new object[] { validPendingPdfId }, Ct))
            .Should().NotBeNull("valid-game PDF must survive cleanup");
        (await _dbContext.PdfDocuments.FindAsync(new object[] { validReadyPdfId }, Ct))
            .Should().NotBeNull("valid-game PDF must survive cleanup");

        // Assert — only the valid Pending PDF is re-enqueued (exactly one Queued job)
        (await _dbContext.ProcessingJobs.CountAsync(
                j => j.PdfDocumentId == validPendingPdfId && j.Status == "Queued", Ct))
            .Should().Be(1, "valid Pending PDF with no active job must be re-enqueued");
        (await _dbContext.ProcessingJobs.CountAsync(j => j.PdfDocumentId == validReadyPdfId, Ct))
            .Should().Be(0, "a Ready PDF must not be re-enqueued");
    }

    [Fact]
    public async Task Cleanup_IsIdempotent_OnSecondRun()
    {
        var orphanPdfId = await SeedPdfWithChildrenAsync(SoftDeletedGameId, "Ready");
        var validPdfId = await SeedPdfWithChildrenAsync(ValidGameId, "Ready");

        await OrphanPdfCleanupSeeder.CleanupAsync(_dbContext!, _mediator!, NullLogger.Instance, Ct);
        // Second run must be a safe no-op (the orphan is already gone).
        await OrphanPdfCleanupSeeder.CleanupAsync(_dbContext!, _mediator!, NullLogger.Instance, Ct);

        (await _dbContext!.PdfDocuments.FindAsync(new object[] { orphanPdfId }, Ct)).Should().BeNull();
        (await _dbContext.PdfDocuments.FindAsync(new object[] { validPdfId }, Ct)).Should().NotBeNull();
    }

    [Fact]
    [Trait("Issue", "3075")]
    public async Task Cleanup_RemovesLegacyShell_KeepsModernSiblingWithChunks()
    {
        // A legacy-scheme empty shell (seed/ path, 0 chunks) with a VALID parent, alongside the
        // modern Ready sibling that holds the real chunks/vectors for the same game (#3075).
        var legacyShellId = await SeedLegacyShellPdfAsync(ValidGameId);
        var modernSiblingId = await SeedPdfWithChildrenAsync(ValidGameId, "Ready");

        await OrphanPdfCleanupSeeder.CleanupAsync(_dbContext!, _mediator!, NullLogger.Instance, Ct);

        // The legacy shell is hard-deleted (a valid parent means #2907 alone would miss it)…
        (await _dbContext!.PdfDocuments.FindAsync(new object[] { legacyShellId }, Ct))
            .Should().BeNull("legacy-scheme empty shell must be hard-deleted (#3075)");
        // …while the modern sibling and its chunks survive (still serving RAG).
        (await _dbContext.PdfDocuments.FindAsync(new object[] { modernSiblingId }, Ct))
            .Should().NotBeNull("modern sibling with chunks must survive");
        (await _dbContext.TextChunks.CountAsync(c => c.PdfDocumentId == modernSiblingId, Ct))
            .Should().Be(2, "modern sibling's chunks must be preserved");
    }

    // ── seeding helpers ──────────────────────────────────────────────────

    private async Task<Guid> SeedPdfWithChildrenAsync(Guid sharedGameId, string processingState)
    {
        var pdfId = Guid.NewGuid();

        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            UploadedByUserId = SystemUserId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = $"pdfs/{pdfId:N}/file.pdf",
            FileSizeBytes = 2048,
            ProcessingState = processingState,
            UploadedAt = DateTime.UtcNow,
            PageCount = 4,
        });

        for (var i = 0; i < 2; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                SharedGameId = sharedGameId,
                Content = $"Chunk {i}",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 8,
                CreatedAt = DateTime.UtcNow,
            });
        }

        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            GameId = sharedGameId,
            ChunkCount = 2,
            TotalCharacters = 16,
            IndexedAt = DateTime.UtcNow,
            IndexingStatus = "completed",
            EmbeddingModel = "test-model",
            EmbeddingDimensions = 384,
        });

        await _dbContext.SaveChangesAsync(Ct);
        return pdfId;
    }

    /// <summary>
    /// Seeds a legacy-scheme empty shell (#3075): a pre-migration <c>seed/…</c> FilePath with
    /// zero text_chunks / vector_documents. Same required-column set as <see cref="SeedPdfWithChildrenAsync"/>.
    /// </summary>
    private async Task<Guid> SeedLegacyShellPdfAsync(Guid sharedGameId)
    {
        var pdfId = Guid.NewGuid();
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            UploadedByUserId = SystemUserId,
            FileName = $"rulebook-{pdfId:N}.pdf",
            FilePath = $"seed/badsworm/{pdfId:N}/rulebook.pdf",
            FileSizeBytes = 0,
            ProcessingState = "Failed",
            UploadedAt = DateTime.UtcNow,
            PageCount = 0,
        });
        await _dbContext.SaveChangesAsync(Ct);
        return pdfId;
    }

    private async Task SeedBaseEntitiesAsync()
    {
        if (!await _dbContext!.Users.AnyAsync(u => u.Id == SystemUserId, Ct))
        {
            _dbContext.Users.Add(new UserEntity
            {
                Id = SystemUserId,
                Email = "seed-orphan@meepleai.dev",
                DisplayName = "SeedSystem",
                Role = "Admin",
                Tier = "Free",
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (!await _dbContext.SharedGames.AnyAsync(g => g.Id == ValidGameId, Ct))
        {
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = ValidGameId,
                Title = "Valid Catalog Game",
                CreatedAt = DateTime.UtcNow,
            });
        }

        // A soft-deleted parent: its row exists but the !IsDeleted filter hides it, so PDFs
        // pointing at it are treated as orphans by #2907.
        if (!await _dbContext.SharedGames.IgnoreQueryFilters().AnyAsync(g => g.Id == SoftDeletedGameId, Ct))
        {
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = SoftDeletedGameId,
                Title = "Soft-Deleted Game",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = true,
            });
        }

        await _dbContext.SaveChangesAsync(Ct);
    }
}
