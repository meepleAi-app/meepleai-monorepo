using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for RagAccessService.
/// Ownership/RAG access feature: cascading access rules (admin → public → ownership).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagAccessServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid PrivateGameId = Guid.NewGuid();

    #region CanAccessRagAsync Tests

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task CanAccessRagAsync_AdminRole_ReturnsTrue(UserRole role)
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: false);
        var service = new RagAccessService(db);

        // Act
        var result = await service.CanAccessRagAsync(UserId, GameId, role);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessRagAsync_IsRagPublicTrue_ReturnsTrueWithoutOwnership()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: true);
        var service = new RagAccessService(db);

        // Act — regular user, no ownership declared
        var result = await service.CanAccessRagAsync(UserId, GameId, UserRole.User);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessRagAsync_HasDeclaredOwnership_ReturnsTrue()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: false);
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SharedGameId = GameId,
            OwnershipDeclaredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new RagAccessService(db);

        // Act
        var result = await service.CanAccessRagAsync(UserId, GameId, UserRole.User);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessRagAsync_NoAdminNoPublicNoOwnership_ReturnsFalse()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: false);
        // Add library entry WITHOUT ownership declared
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SharedGameId = GameId,
            OwnershipDeclaredAt = null
        });
        await db.SaveChangesAsync();
        var service = new RagAccessService(db);

        // Act
        var result = await service.CanAccessRagAsync(UserId, GameId, UserRole.User);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAccessibleKbCardsAsync Tests

    [Fact]
    public async Task GetAccessibleKbCardsAsync_WithAccess_ReturnsCompletedVectorDocumentIds()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: true);
        var completedDocId = Guid.NewGuid();
        var pendingDocId = Guid.NewGuid();

        db.VectorDocuments.AddRange(
            new VectorDocumentEntity
            {
                Id = completedDocId,
                SharedGameId = GameId,
                PdfDocumentId = Guid.NewGuid(),
                IndexingStatus = "completed"
            },
            new VectorDocumentEntity
            {
                Id = pendingDocId,
                SharedGameId = GameId,
                PdfDocumentId = Guid.NewGuid(),
                IndexingStatus = "pending"
            });
        await db.SaveChangesAsync();
        var service = new RagAccessService(db);

        // Act
        var result = await service.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.User);

        // Assert
        result.Should().ContainSingle();
        result.Should().Contain(completedDocId);
        result.Should().NotContain(pendingDocId);
    }

    [Fact]
    public async Task GetAccessibleKbCardsAsync_WithoutAccess_ReturnsEmptyList()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: false);
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = Guid.NewGuid(),
            IndexingStatus = "completed"
        });
        await db.SaveChangesAsync();
        var service = new RagAccessService(db);

        // Act — regular user, no ownership
        var result = await service.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.User);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CanAccessRagAsync - PrivateGame (Issue #4137)

    // Before the PrivateGame branch existed, every one of these returned false:
    // rules 2 and 3 only consult SharedGames and UserLibraryEntry.SharedGameId, so a
    // PrivateGame id fell through all of them — including for its owner.

    [Fact]
    public async Task CanAccessRagAsync_OwnerOfPrivateGame_ReturnsTrue()
    {
        var db = CreateDbWithPrivateGame(ownerId: UserId);
        var service = new RagAccessService(db);

        var result = await service.CanAccessRagAsync(UserId, PrivateGameId, UserRole.User);

        result.Should().BeTrue();
    }

    // The other direction, and the one that matters: granting the owner must not
    // grant everyone. A single-direction test would stay green if the branch
    // dropped the OwnerId predicate.
    [Fact]
    public async Task CanAccessRagAsync_OtherUsersPrivateGame_ReturnsFalse()
    {
        var db = CreateDbWithPrivateGame(ownerId: Guid.NewGuid());
        var service = new RagAccessService(db);

        var result = await service.CanAccessRagAsync(UserId, PrivateGameId, UserRole.User);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessRagAsync_SoftDeletedPrivateGame_ReturnsFalseEvenForOwner()
    {
        var db = CreateDbWithPrivateGame(ownerId: UserId, isDeleted: true);
        var service = new RagAccessService(db);

        var result = await service.CanAccessRagAsync(UserId, PrivateGameId, UserRole.User);

        result.Should().BeFalse();
    }

    // DECISION, asserted on purpose (Issue #4137): an admin CAN query another
    // user's private PDFs, for support and diagnosis. It falls out of rule 1,
    // which short-circuits before any ownership check — but it is a choice, not
    // an oversight, so it is pinned here. If it is ever reversed, this test goes
    // red and the reversal is a decision rather than a silent change.
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task CanAccessRagAsync_AdminOnAnotherUsersPrivateGame_ReturnsTrue(UserRole role)
    {
        var db = CreateDbWithPrivateGame(ownerId: Guid.NewGuid());
        var service = new RagAccessService(db);

        var result = await service.CanAccessRagAsync(UserId, PrivateGameId, role);

        result.Should().BeTrue();
    }

    // The KB-card listing already matched on VectorDocument.GameId, so the only
    // thing that kept a private corpus unreachable was the access check. This
    // asserts the whole path, not just the predicate.
    [Fact]
    public async Task GetAccessibleKbCardsAsync_OwnerOfPrivateGame_ReturnsItsCompletedDocuments()
    {
        var db = CreateDbWithPrivateGame(ownerId: UserId);
        var docId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = docId,
            GameId = PrivateGameId,
            IndexingStatus = "completed"
        });
        await db.SaveChangesAsync();
        var service = new RagAccessService(db);

        var result = await service.GetAccessibleKbCardsAsync(UserId, PrivateGameId, UserRole.User);

        result.Should().ContainSingle().Which.Should().Be(docId);
    }

    #endregion

    #region Helpers

    private static MeepleAiDbContext CreateDbWithSharedGame(bool isRagPublic)
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = GameId,
            Title = "Test Game",
            IsRagPublic = isRagPublic,
            IsDeleted = false,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>
    /// A PrivateGame and nothing else: no SharedGame row, no library entry. That is
    /// the shape that used to make every rule miss (Issue #4137).
    /// </summary>
    private static MeepleAiDbContext CreateDbWithPrivateGame(Guid ownerId, bool isDeleted = false)
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = PrivateGameId,
            OwnerId = ownerId,
            Title = "Private Test Game",
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return db;
    }

    #endregion
}
