using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="RagAccessService.GetAccessibleGameIdsAsync"/>.
/// Issue #1661: RBAC foundation for cross-game KB search and SSE ask.
/// Rules:
///   - Admin or SuperAdmin → all non-deleted SharedGame IDs.
///   - Regular user → union of (public games: IsRagPublic=true) ∪ (owned games: OwnershipDeclaredAt != null).
///   - Deleted games excluded for all roles.
///   - OwnershipDeclaredAt == null → excluded (EC-8).
///   - User with no accessible games → empty list, no error (EC-1).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagAccessServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>Creates a fresh in-memory DbContext with a unique DB name.</summary>
    private static MeepleAiDbContext CreateDb() =>
        TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());

    private static IRagAccessService CreateSut(MeepleAiDbContext db) =>
        new RagAccessService(db);

    // ─────────────────────────────────────── helpers ──────────────────────────

    private static SharedGameEntity MakeSharedGame(
        Guid id,
        bool isRagPublic = false,
        bool isDeleted = false) => new()
    {
        Id = id,
        Title = $"Game-{id:N}",
        Description = string.Empty,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        Status = 1,
        GameDataStatus = 5,
        IsRagPublic = isRagPublic,
        IsDeleted = isDeleted,
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    private static UserLibraryEntryEntity MakeLibraryEntry(
        Guid userId,
        Guid sharedGameId,
        DateTime? ownershipDeclaredAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SharedGameId = sharedGameId,
        OwnershipDeclaredAt = ownershipDeclaredAt,
    };

    // ─────────────────────────── Admin / SuperAdmin ───────────────────────────

    [Fact]
    public async Task AdminRole_ReturnsAllNonDeletedGames()
    {
        // Arrange
        await using var db = CreateDb();
        var game1 = MakeSharedGame(Guid.NewGuid(), isRagPublic: false);
        var game2 = MakeSharedGame(Guid.NewGuid(), isRagPublic: true);
        var deleted = MakeSharedGame(Guid.NewGuid(), isDeleted: true);
        db.SharedGames.AddRange(game1, game2, deleted);
        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // Act
        var result = await sut.GetAccessibleGameIdsAsync(Guid.NewGuid(), UserRole.Admin, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2)
            .And.Contain(game1.Id)
            .And.Contain(game2.Id)
            .And.NotContain(deleted.Id);
    }

    [Fact]
    public async Task SuperAdminRole_ReturnsAllNonDeletedGames()
    {
        // Arrange
        await using var db = CreateDb();
        var game1 = MakeSharedGame(Guid.NewGuid(), isRagPublic: false);
        var game2 = MakeSharedGame(Guid.NewGuid(), isRagPublic: true);
        var deleted = MakeSharedGame(Guid.NewGuid(), isDeleted: true);
        db.SharedGames.AddRange(game1, game2, deleted);
        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // Act
        var result = await sut.GetAccessibleGameIdsAsync(Guid.NewGuid(), UserRole.SuperAdmin, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2)
            .And.Contain(game1.Id)
            .And.Contain(game2.Id)
            .And.NotContain(deleted.Id);
    }

    // ─────────────────────────── Regular user ─────────────────────────────────

    [Fact]
    public async Task RegularUser_ReturnsPublicAndOwnedGamesUnion()
    {
        // Arrange — 3 games: public, owned-by-user, owned-only-by-another-user (not public)
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var publicGame = MakeSharedGame(Guid.NewGuid(), isRagPublic: true);
        var ownedGame = MakeSharedGame(Guid.NewGuid(), isRagPublic: false);
        var otherUserGame = MakeSharedGame(Guid.NewGuid(), isRagPublic: false); // not public, not owned by userId

        db.SharedGames.AddRange(publicGame, ownedGame, otherUserGame);

        // userId owns ownedGame
        db.UserLibraryEntries.Add(MakeLibraryEntry(userId, ownedGame.Id, DateTime.UtcNow));
        // otherUser owns otherUserGame — should NOT appear for userId
        db.UserLibraryEntries.Add(MakeLibraryEntry(otherUserId, otherUserGame.Id, DateTime.UtcNow));

        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // Act
        var result = await sut.GetAccessibleGameIdsAsync(userId, UserRole.User, TestCancellationToken);

        // Assert: publicGame + ownedGame; otherUserGame excluded
        result.Should().HaveCount(2)
            .And.Contain(publicGame.Id)
            .And.Contain(ownedGame.Id)
            .And.NotContain(otherUserGame.Id);
    }

    [Fact]
    public async Task OwnedWithoutOwnershipDeclaredAt_IsExcluded()
    {
        // Arrange — EC-8: library entry exists but OwnershipDeclaredAt == null
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var nonPublicGame = MakeSharedGame(Guid.NewGuid(), isRagPublic: false);

        db.SharedGames.Add(nonPublicGame);
        // OwnershipDeclaredAt = null → should be excluded
        db.UserLibraryEntries.Add(MakeLibraryEntry(userId, nonPublicGame.Id, ownershipDeclaredAt: null));
        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // Act
        var result = await sut.GetAccessibleGameIdsAsync(userId, UserRole.User, TestCancellationToken);

        // Assert: nothing accessible (not public, ownership not declared)
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UserWith0AccessibleGames_ReturnsEmpty()
    {
        // Arrange — EC-1: brand new user, no library, no public games
        await using var db = CreateDb();
        var userId = Guid.NewGuid();

        // A private-only game exists but it belongs to another user
        var privateGame = MakeSharedGame(Guid.NewGuid(), isRagPublic: false);
        db.SharedGames.Add(privateGame);
        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // Act
        var result = await sut.GetAccessibleGameIdsAsync(userId, UserRole.User, TestCancellationToken);

        // Assert: no error, empty list
        result.Should().NotBeNull()
            .And.BeEmpty();
    }

    [Fact]
    public async Task DeletedGames_AreExcludedForAllRoles()
    {
        // Arrange — deleted game that is also public AND owned
        await using var db = CreateDb();
        var userId = Guid.NewGuid();

        var deletedPublic = MakeSharedGame(Guid.NewGuid(), isRagPublic: true, isDeleted: true);
        var deletedOwned = MakeSharedGame(Guid.NewGuid(), isRagPublic: false, isDeleted: true);
        db.SharedGames.AddRange(deletedPublic, deletedOwned);

        db.UserLibraryEntries.Add(MakeLibraryEntry(userId, deletedOwned.Id, DateTime.UtcNow));
        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // User role
        var userResult = await sut.GetAccessibleGameIdsAsync(userId, UserRole.User, TestCancellationToken);
        userResult.Should().BeEmpty("deleted games must be excluded even when public or owned");

        // Admin role
        var adminResult = await sut.GetAccessibleGameIdsAsync(Guid.NewGuid(), UserRole.Admin, TestCancellationToken);
        adminResult.Should().BeEmpty("deleted games must be excluded for Admin too");
    }

    [Fact]
    public async Task Result_IsDistinct_WhenGameIsPublicAndOwned()
    {
        // Arrange — same game is both public AND in the user's owned library
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var game = MakeSharedGame(Guid.NewGuid(), isRagPublic: true);

        db.SharedGames.Add(game);
        db.UserLibraryEntries.Add(MakeLibraryEntry(userId, game.Id, DateTime.UtcNow));
        await db.SaveChangesAsync(TestCancellationToken);

        var sut = CreateSut(db);

        // Act
        var result = await sut.GetAccessibleGameIdsAsync(userId, UserRole.User, TestCancellationToken);

        // Assert: appears exactly once
        result.Should().HaveCount(1)
            .And.Contain(game.Id);
    }
}
