using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Integration;

/// <summary>
/// Integration tests for ProposalMigration flow: Approve → Fetch Pending → Choose.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class ProposalMigrationFlowIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private ProposalMigrationRepository _migrationRepo = null!;
    private PrivateGameRepository _privateGameRepo = null!;
    private readonly Mock<IDomainEventCollector> _eventCollector;

    private Guid _userId;
    private Guid _privateGameId;
    private Guid _sharedGameId;
    private Guid _shareRequestId;
    private Guid _libraryEntryId;

    public ProposalMigrationFlowIntegrationTests()
    {
        _eventCollector = TestDbContextFactory.CreateMockEventCollector();
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique database for test isolation
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"ProposalMigrationFlowTest_{Guid.NewGuid()}");
        _migrationRepo = new ProposalMigrationRepository(_dbContext);
        _privateGameRepo = new PrivateGameRepository(_dbContext, _eventCollector.Object);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        _userId = Guid.NewGuid();
        _privateGameId = Guid.NewGuid();
        _sharedGameId = Guid.NewGuid();
        _shareRequestId = Guid.NewGuid();
        _libraryEntryId = Guid.NewGuid();

        // User
        var user = new UserEntity
        {
            Id = _userId,
            Email = $"user_{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);

        // PrivateGame
        var privateGame = new PrivateGameEntity
        {
            Id = _privateGameId,
            OwnerId = _userId,
            BggId = 12345,
            Title = "My Private Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = PrivateGameSource.BoardGameGeek,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _dbContext.PrivateGames.Add(privateGame);

        // SharedGame (created by approval)
        var sharedGame = new SharedGameEntity
        {
            Id = _sharedGameId,
            BggId = 12345,
            Title = "My Private Game (Now Shared)",
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _dbContext.SharedGames.Add(sharedGame);

        // UserLibraryEntry (links to PrivateGame)
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = _libraryEntryId,
            UserId = _userId,
            PrivateGameId = _privateGameId,
            SharedGameId = null,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.UserLibraryEntries.Add(libraryEntry);

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPendingMigrations_WhenPendingExists_ReturnsCorrectCount()
    {
        // Arrange: Create a pending migration
        var migration = ProposalMigration.Create(_shareRequestId, _privateGameId, _sharedGameId, _userId);
        await _migrationRepo.AddAsync(migration);

        // Act
        var pending = await _migrationRepo.GetPendingByUserIdAsync(_userId);

        // Assert
        pending.Should().HaveCount(1);
        pending[0].Id.Should().Be(migration.Id);
        pending[0].Choice.Should().Be(PostApprovalMigrationChoice.Pending);
    }

    [Fact]
    public async Task ChooseLinkToCatalog_UpdatesLibraryEntryAndDeletesPrivateGame()
    {
        // Arrange: Create migration
        var migration = ProposalMigration.Create(_shareRequestId, _privateGameId, _sharedGameId, _userId);
        await _migrationRepo.AddAsync(migration);

        // Act: Choose LinkToCatalog
        migration.ChooseLinkToCatalog();
        await _migrationRepo.UpdateAsync(migration);

        // Simulate handler logic
        var libraryEntry = await _dbContext.UserLibraryEntries.FirstAsync(e => e.PrivateGameId == _privateGameId);
        libraryEntry.SharedGameId = _sharedGameId;
        libraryEntry.PrivateGameId = null;

        var privateGame = await _privateGameRepo.GetByIdAsync(_privateGameId);
        privateGame!.Delete();
        await _privateGameRepo.UpdateAsync(privateGame);

        await _dbContext.SaveChangesAsync();

        // Assert: Verify migration updated
        var updatedMigration = await _migrationRepo.GetByIdAsync(migration.Id);
        updatedMigration.Should().NotBeNull();
        updatedMigration!.Choice.Should().Be(PostApprovalMigrationChoice.LinkToCatalog);
        updatedMigration.ChoiceAt.Should().NotBeNull();

        // Assert: Verify library entry updated
        var updatedEntry = await _dbContext.UserLibraryEntries.FirstAsync(e => e.Id == _libraryEntryId);
        updatedEntry.SharedGameId.Should().Be(_sharedGameId);
        updatedEntry.PrivateGameId.Should().BeNull();

        // Assert: Verify private game deleted
        var deletedGame = await _dbContext.PrivateGames.FirstAsync(g => g.Id == _privateGameId);
        deletedGame.IsDeleted.Should().BeTrue();
        deletedGame.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ChooseKeepPrivate_NoChangesToLibraryEntryOrPrivateGame()
    {
        // Arrange: Create migration
        var migration = ProposalMigration.Create(_shareRequestId, _privateGameId, _sharedGameId, _userId);
        await _migrationRepo.AddAsync(migration);

        // Act: Choose KeepPrivate
        migration.ChooseKeepPrivate();
        await _migrationRepo.UpdateAsync(migration);
        await _dbContext.SaveChangesAsync();

        // Assert: Verify migration updated
        var updatedMigration = await _migrationRepo.GetByIdAsync(migration.Id);
        updatedMigration.Should().NotBeNull();
        updatedMigration!.Choice.Should().Be(PostApprovalMigrationChoice.KeepPrivate);
        updatedMigration.ChoiceAt.Should().NotBeNull();

        // Assert: Verify library entry unchanged
        var libraryEntry = await _dbContext.UserLibraryEntries.FirstAsync(e => e.Id == _libraryEntryId);
        libraryEntry.PrivateGameId.Should().Be(_privateGameId);
        libraryEntry.SharedGameId.Should().BeNull();

        // Assert: Verify private game not deleted
        var privateGame = await _dbContext.PrivateGames.FirstAsync(g => g.Id == _privateGameId);
        privateGame.IsDeleted.Should().BeFalse();
        privateGame.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingMigrations_AfterChoice_ReturnsEmpty()
    {
        // Arrange: Create migration and choose
        var migration = ProposalMigration.Create(_shareRequestId, _privateGameId, _sharedGameId, _userId);
        await _migrationRepo.AddAsync(migration);

        migration.ChooseLinkToCatalog();
        await _migrationRepo.UpdateAsync(migration);

        // Act
        var pending = await _migrationRepo.GetPendingByUserIdAsync(_userId);

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByShareRequestId_ReturnsCorrectMigration()
    {
        // Arrange
        var migration = ProposalMigration.Create(_shareRequestId, _privateGameId, _sharedGameId, _userId);
        await _migrationRepo.AddAsync(migration);

        // Act
        var result = await _migrationRepo.GetByShareRequestIdAsync(_shareRequestId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(migration.Id);
        result.ShareRequestId.Should().Be(_shareRequestId);
    }
}
