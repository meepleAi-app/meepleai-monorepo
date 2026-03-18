using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;

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
        Assert.True(result);
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
        Assert.True(result);
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
        Assert.True(result);
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
        Assert.False(result);
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
        Assert.Single(result);
        Assert.Contains(completedDocId, result);
        Assert.DoesNotContain(pendingDocId, result);
    }

    [Fact]
    public async Task GetAccessibleKbCardsAsync_WithoutAccess_ReturnsEmptyList()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: false);
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = GameId,
            PdfDocumentId = Guid.NewGuid(),
            IndexingStatus = "completed"
        });
        await db.SaveChangesAsync();
        var service = new RagAccessService(db);

        // Act — regular user, no ownership
        var result = await service.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.User);

        // Assert
        Assert.Empty(result);
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

    #endregion
}
