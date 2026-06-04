using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for the backfill SQL in migration AddSharedGamePdfCoverR2Key.
/// Issue #1852: Cover Stack L4 — pdf_cover_r2_key column on shared_games.
///
/// Verifies that the UPDATE...FROM backfill is idempotent and correctly handles:
///   1. Propagation from a Generated PDF to a NULL shared-game column.
///   2. No overwrite when the shared-game column is already populated.
///   3. Skipping PDFs whose cover_generation_status is not 'Generated'.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1852")]
public sealed class AddSharedGamePdfCoverR2KeyMigrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;

    // Mirrors the production backfill in migration 20260603120937_AddSharedGamePdfCoverR2Key.
    // Issue #1885: shared_games has no updated_at column (entity uses ModifiedAt, set only on
    // user-driven updates, not backfill). The column reference is snake_case post-rename via
    // migration 20260604130156_RenamePdfDocumentsSharedGameIdToSnakeCase.
    private const string BackfillSql = @"
        UPDATE shared_games sg
        SET pdf_cover_r2_key = pd.cover_r2_key
        FROM pdf_documents pd
        WHERE pd.shared_game_id = sg.id
          AND pd.cover_generation_status = 'Generated'
          AND pd.cover_r2_key IS NOT NULL
          AND sg.pdf_cover_r2_key IS NULL;
    ";

    public AddSharedGamePdfCoverR2KeyMigrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"migration_cover_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Backfill_PopulatesSharedGameFromGeneratedPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        await _dbContext.SaveChangesAsync();

        _dbContext.SharedGames.Add(CreateSharedGame(gameId, userId, pdfCoverR2Key: null));
        await _dbContext.SaveChangesAsync();

        _dbContext.PdfDocuments.Add(CreatePdfDocument(pdfId, gameId, userId,
            coverGenerationStatus: "Generated",
            coverR2Key: "pdf-cover-abc"));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        await _dbContext.Database.ExecuteSqlRawAsync(BackfillSql);
        _dbContext.ChangeTracker.Clear();

        // Assert
        var sg = await _dbContext.SharedGames.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId);
        sg.Should().NotBeNull();
        sg!.PdfCoverR2Key.Should().Be("pdf-cover-abc");
    }

    [Fact]
    public async Task Backfill_SkipsAlreadyPopulatedSharedGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        await _dbContext.SaveChangesAsync();

        _dbContext.SharedGames.Add(CreateSharedGame(gameId, userId, pdfCoverR2Key: "existing-key"));
        await _dbContext.SaveChangesAsync();

        _dbContext.PdfDocuments.Add(CreatePdfDocument(pdfId, gameId, userId,
            coverGenerationStatus: "Generated",
            coverR2Key: "different-key"));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        await _dbContext.Database.ExecuteSqlRawAsync(BackfillSql);
        _dbContext.ChangeTracker.Clear();

        // Assert — the pre-existing key must not be overwritten
        var sg = await _dbContext.SharedGames.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId);
        sg.Should().NotBeNull();
        sg!.PdfCoverR2Key.Should().Be("existing-key");
    }

    [Fact]
    public async Task Backfill_IgnoresSkippedAndFailedStatuses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Users.Add(CreateUser(userId));
        await _dbContext.SaveChangesAsync();

        _dbContext.SharedGames.Add(CreateSharedGame(gameId, userId, pdfCoverR2Key: null));
        await _dbContext.SaveChangesAsync();

        _dbContext.PdfDocuments.Add(CreatePdfDocument(pdfId, gameId, userId,
            coverGenerationStatus: "Skipped",
            coverR2Key: "should-not-propagate"));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        await _dbContext.Database.ExecuteSqlRawAsync(BackfillSql);
        _dbContext.ChangeTracker.Clear();

        // Assert — column must remain NULL
        var sg = await _dbContext.SharedGames.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId);
        sg.Should().NotBeNull();
        sg!.PdfCoverR2Key.Should().BeNull();
    }

    // ---------- seed helpers ----------

    private static UserEntity CreateUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"backfill-test-{userId:N}@meepleai.test",
        DisplayName = "Backfill Test User",
        PasswordHash = "test-hash",
        Role = "user",
        Tier = "free",
        CreatedAt = DateTime.UtcNow
    };

    private static SharedGameEntity CreateSharedGame(Guid gameId, Guid createdBy, string? pdfCoverR2Key) => new()
    {
        Id = gameId,
        Title = $"Test Game {gameId:N}",
        YearPublished = 2024,
        Description = "Integration test game for migration backfill",
        MinPlayers = 1,
        MaxPlayers = 4,
        PlayingTimeMinutes = 60,
        MinAge = 10,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        CreatedBy = createdBy,
        CreatedAt = DateTime.UtcNow,
        PdfCoverR2Key = pdfCoverR2Key
    };

    private static PdfDocumentEntity CreatePdfDocument(
        Guid pdfId,
        Guid sharedGameId,
        Guid uploadedByUserId,
        string coverGenerationStatus,
        string? coverR2Key) => new()
    {
        Id = pdfId,
        SharedGameId = sharedGameId,
        UploadedByUserId = uploadedByUserId,
        FileName = $"test-{pdfId:N}.pdf",
        FilePath = $"/test/{pdfId:N}.pdf",
        FileSizeBytes = 1024,
        ContentType = "application/pdf",
        UploadedAt = DateTime.UtcNow,
        ProcessingState = "Ready",
        CoverGenerationStatus = coverGenerationStatus,
        CoverR2Key = coverR2Key
    };
}
