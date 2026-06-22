using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Repositories;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// Integration tests for <see cref="GameBookRepository"/> against a real PostgreSQL
/// database via Testcontainers. Verifies aggregate persistence including the
/// <see cref="GameRef"/> owned-type mapping (Phase A4), <see cref="GameBookRole"/>
/// flags enum, and the unique-kb-source filter for community books.
/// Phase A5 of the Gamebook multi-book generalization (2026-05-19).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameBookRepositoryTests : IClassFixture<SharedTestcontainersFixture>, IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName;
    private string? _connectionString;

    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public GameBookRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _databaseName = $"test_gamebook_repo_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Create schema from current EF model (Phase A5 has no migration yet — A6 generates it).
        // EnsureCreatedAsync materializes the model graph including the game_books table
        // configured by GameBookEntityConfiguration.
        using var dbContext = _fixture.CreateDbContext(_connectionString);
        await dbContext.Database.EnsureCreatedAsync();

        // Seed admin user so PdfDocumentEntity FK to users can be satisfied
        // for the FindCommunityByKbSourceAsync test.
        if (!await dbContext.Users.AnyAsync(u => u.Id == AdminUserId))
        {
            dbContext.Users.Add(new UserEntity
            {
                Id = AdminUserId,
                Email = "gamebook-repo-test@meepleai.test",
                DisplayName = "GameBook Repo Test Admin",
                PasswordHash = "test-hash",
                Role = "admin",
                Tier = "free",
                CreatedAt = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrip()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new GameBookRepository(dbContext);

        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()),
            "Press Start",
            GameBookRole.Tutorial | GameBookRole.Setup,
            ParagraphScheme.None,
            language: "en",
            sequentialRead: false,
            kbSourceDocId: null,
            physicalOnly: true,
            createdBy: AdminUserId);

        // Act
        await repository.AddAsync(book, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        using var verifyContext = _fixture.CreateDbContext(_connectionString!);
        var verifyRepo = new GameBookRepository(verifyContext);
        var loaded = await verifyRepo.GetByIdAsync(book.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.DisplayName.Should().Be("Press Start");
        loaded.Roles.Should().Be(GameBookRole.Tutorial | GameBookRole.Setup);
        loaded.PhysicalOnly.Should().BeTrue();
    }

    [Fact]
    public async Task FindCommunityByKbSourceAsync_ReturnsBookWhenExists()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var pdfId = await SeedPdfDocumentAsync(dbContext);

        var repository = new GameBookRepository(dbContext);
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()),
            "Rules",
            GameBookRole.RulesReference,
            ParagraphScheme.None,
            language: "en",
            sequentialRead: false,
            kbSourceDocId: pdfId,
            physicalOnly: false,
            createdBy: AdminUserId);

        // Act
        await repository.AddAsync(book, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        using var verifyContext = _fixture.CreateDbContext(_connectionString!);
        var verifyRepo = new GameBookRepository(verifyContext);
        var found = await verifyRepo.FindCommunityByKbSourceAsync(pdfId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(book.Id);
    }

    /// <summary>
    /// Seeds a PdfDocumentEntity (with required SharedGameEntity parent) so it can
    /// be referenced as a GameBook.KbSourceDocId. The IGameBookRepository contract
    /// does not enforce DB-level FK on kb_source_doc_id, but the kb-source-conflict
    /// flow tests assume a real PDF exists.
    /// </summary>
    private async Task<Guid> SeedPdfDocumentAsync(Api.Infrastructure.MeepleAiDbContext dbContext)
    {
        var sharedGameId = Guid.NewGuid();
        dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = $"GameBook Repo Test Game {sharedGameId:N}",
            Description = "Test fixture for GameBookRepository integration tests",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            YearPublished = 2024,
            Status = 1,
            CreatedBy = AdminUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });

        var pdfId = Guid.NewGuid();
        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            UploadedByUserId = AdminUserId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = $"/test/test-{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return pdfId;
    }
}
