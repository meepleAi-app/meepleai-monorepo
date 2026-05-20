using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Smoke;

/// <summary>
/// Integration tests for the KB reindex and RAPTOR rebuild lifecycle.
/// Issue #903: SG2 — KB lifecycle con re-index smoke test.
///
/// Covers:
/// - SG2-T1: Reindex happy path — returns 202 with jobId + status "completed"
/// - SG2-T2: RAPTOR rebuild — free-tier user receives TierFeatureLockedException
/// - SG2-T3: DeletePdf cascade — deletes orphan raptor_summaries rows
/// - SG2-T4: RAPTOR rebuild — premium user, no indexed PDFs → completed + pdfCount=0
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
public sealed class KbReindexIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Stable test IDs — SG2-specific, no collision with other smoke tests
    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000902");
    private static readonly Guid TestGameId = new("20000000-0000-0000-0000-000000000902");

    public KbReindexIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_kbreindex_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations (retry on transient container startup delays)
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        // Seed required parent entities (User, Game) for FK constraints
        _dbContext.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "smoke-sg2-kbreindex@test.meepleai",
            PasswordHash = "v1.test-hash",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = TestGameId,
            Title = "SG2 KB Reindex Test Game",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

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

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    // ─── SG2-T1 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG2-T1: Reindex happy path — game with no indexable PDFs returns completed + pdfCount=0.
    ///
    /// Rationale: Calling reindex on a game with no indexed PDFs is a valid (idempotent)
    /// operation. The command returns 202 with status="completed" and pdfCount=0.
    /// This test validates the command response shape without triggering the full
    /// embedding pipeline (which requires external services not available in integration tests).
    /// </summary>
    [Fact]
    public async Task Reindex_WithNoPdfs_ReturnsJobIdAndCompletedStatus()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new ReindexGameKbCommand(GameId: TestGameId, UserId: TestUserId);

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert — response shape matches SG2 contract
        result.Should().NotBeNull();
        result.JobId.Should().NotBe(Guid.Empty);
        result.Status.Should().Be("completed");
        result.PdfCount.Should().Be(0);
    }

    // ─── SG2-T2 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG2-T2: RAPTOR rebuild — free-tier user throws TierFeatureLockedException.
    ///
    /// The ITierEnforcementService mock in IntegrationServiceCollectionBuilder returns
    /// TierLimits.Unlimited by default (all features allowed). This test overrides the
    /// mock to return FreeTier limits, which block RAPTOR rebuild.
    ///
    /// We build a fresh service provider with a tier mock that returns FreeTier.
    /// </summary>
    [Fact]
    public async Task RebuildRaptor_FreeTier_ThrowsTierFeatureLockedException()
    {
        // Arrange — build a fresh scope with a free-tier enforcement mock
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Override the tier mock to simulate a free-tier user
        var freeTierMock = new Mock<ITierEnforcementService>();
        freeTierMock
            .Setup(t => t.CanPerformAsync(
                It.IsAny<Guid>(),
                TierAction.RaptorRebuild,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // FREE TIER BLOCKED
        freeTierMock
            .Setup(t => t.CanPerformAsync(
                It.IsAny<Guid>(),
                It.Is<TierAction>(a => a != TierAction.RaptorRebuild),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        freeTierMock
            .Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier);

        services.AddScoped<ITierEnforcementService>(_ => freeTierMock.Object);

        var freeTierProvider = services.BuildServiceProvider();
        var mediator = freeTierProvider.GetRequiredService<IMediator>();

        var command = new RebuildRaptorCommand(GameId: TestGameId, UserId: TestUserId);

        // Act & Assert — handler throws TierFeatureLockedException (maps to 403 at endpoint)
        var ex = await Assert.ThrowsAsync<TierFeatureLockedException>(
            () => mediator.Send(command, TestCancellationToken));

        ex.Feature.Should().Be("RaptorRebuild");
        ex.ErrorCode.Should().Be("TIER_FEATURE_LOCKED");
        ex.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        if (freeTierProvider is IDisposable d) d.Dispose();
    }

    // ─── SG2-T3 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG2-T3 (MOST IMPORTANT): DeletePdf cascade removes orphan raptor_summaries.
    ///
    /// Schema correctness test. The raptor_summaries table has a CASCADE FK on
    /// PdfDocumentId → pdf_documents.Id. When a PdfDocument is deleted, all its
    /// RaptorSummary rows must be automatically removed by the database.
    ///
    /// Setup:
    ///   1. Seed a PdfDocument
    ///   2. Seed 3 RaptorSummary rows linked to that PDF
    ///   3. Remove the PdfDocument via EF Core
    ///   4. Assert raptor_summaries WHERE pdf_document_id = X is empty
    /// </summary>
    [Fact]
    public async Task DeletePdf_CascadeDeletesOrphanRaptorSummaries()
    {
        // Arrange — use a unique PdfId per test run to avoid cross-test interference
        var pdfId = Guid.NewGuid();
        var ctx = _dbContext!;

        // Seed a shared game entry (needed for FK in pdf_documents.shared_game_id if set)
        // We use a simple game-only PDF (no shared_game reference) to keep it minimal
        ctx.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "sg2-cascade-test.pdf",
            FilePath = $"/pdfs/{pdfId}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = TestUserId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            // Link via PrivateGameId so we don't need a shared_games row
        });
        await ctx.SaveChangesAsync(TestCancellationToken);

        // Seed 3 RaptorSummary rows referencing this PDF
        for (var i = 0; i < 3; i++)
        {
            ctx.RaptorSummaries.Add(new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                GameId = TestGameId,
                TreeLevel = 1,
                ClusterIndex = i,
                SummaryText = $"SG2 cascade test summary #{i}",
                SourceChunkCount = 5,
                CreatedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync(TestCancellationToken);

        // Pre-assert: 3 summaries exist
        var before = await ctx.RaptorSummaries
            .AsNoTracking()
            .CountAsync(r => r.PdfDocumentId == pdfId, TestCancellationToken);
        before.Should().Be(3, "3 RaptorSummary rows were seeded for PDF {pdfId}");

        // Act — delete the PdfDocument. CASCADE should remove the 3 RaptorSummary rows.
        var pdf = await ctx.PdfDocuments
            .AsTracking()
            .FirstAsync(p => p.Id == pdfId, TestCancellationToken);
        ctx.PdfDocuments.Remove(pdf);
        await ctx.SaveChangesAsync(TestCancellationToken);

        // Assert — raptor_summaries orphans are gone
        var after = await ctx.RaptorSummaries
            .AsNoTracking()
            .CountAsync(r => r.PdfDocumentId == pdfId, TestCancellationToken);

        after.Should().Be(0,
            "RaptorSummary rows must be cascade-deleted when their PdfDocument is removed. " +
            "This verifies the OnDelete(DeleteBehavior.Cascade) FK on RaptorSummaryEntity.PdfDocumentId.");
    }

    // ─── SG2-T4 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG2-T4: RAPTOR rebuild for premium user with no indexed PDFs → completed + pdfCount=0.
    ///
    /// Validates that the RebuildRaptorCommand is idempotent when there are no text chunks
    /// stored for the game. The handler should return status="completed", pdfCount=0.
    ///
    /// The default ITierEnforcementService mock in IntegrationServiceCollectionBuilder
    /// returns TierLimits.Unlimited (RaptorRebuildEnabled=true), so tier gate passes.
    /// IRaptorIndexer is NOT registered in the test DI, so the handler degrades gracefully.
    /// </summary>
    [Fact]
    public async Task RebuildRaptor_PremiumUser_NoIndexedPdfs_ReturnsCompletedWithZeroPdfCount()
    {
        // Arrange
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RebuildRaptorCommand(GameId: TestGameId, UserId: TestUserId);

        // Act — IRaptorIndexer is not in test DI → handler degrades to pdfCount=0 or
        // finds no text chunks → returns completed immediately
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().NotBe(Guid.Empty);
        result.Status.Should().Be("completed");
        result.PdfCount.Should().Be(0);
    }
}
