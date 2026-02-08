using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Unit tests for ProposalMigration domain entity.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public class ProposalMigrationTests
{
    [Fact]
    public void Create_WithValidData_ReturnsProposalMigration()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var migration = ProposalMigration.Create(shareRequestId, privateGameId, sharedGameId, userId);

        // Assert
        Assert.NotEqual(Guid.Empty, migration.Id);
        Assert.Equal(shareRequestId, migration.ShareRequestId);
        Assert.Equal(privateGameId, migration.PrivateGameId);
        Assert.Equal(sharedGameId, migration.SharedGameId);
        Assert.Equal(userId, migration.UserId);
        Assert.Equal(PostApprovalMigrationChoice.Pending, migration.Choice);
        Assert.True(migration.IsPending);
        Assert.Null(migration.ChoiceAt);
    }

    [Fact]
    public void Create_WithEmptyShareRequestId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ProposalMigration.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("shareRequestId", exception.ParamName);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));

        Assert.Equal("userId", exception.ParamName);
    }

    [Fact]
    public void ChooseLinkToCatalog_WhenPending_UpdatesChoiceAndTimestamp()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var beforeChoice = DateTime.UtcNow;

        // Act
        migration.ChooseLinkToCatalog();

        // Assert
        Assert.Equal(PostApprovalMigrationChoice.LinkToCatalog, migration.Choice);
        Assert.NotNull(migration.ChoiceAt);
        Assert.True(migration.ChoiceAt >= beforeChoice);
        Assert.False(migration.IsPending);
    }

    [Fact]
    public void ChooseKeepPrivate_WhenPending_UpdatesChoiceAndTimestamp()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var beforeChoice = DateTime.UtcNow;

        // Act
        migration.ChooseKeepPrivate();

        // Assert
        Assert.Equal(PostApprovalMigrationChoice.KeepPrivate, migration.Choice);
        Assert.NotNull(migration.ChoiceAt);
        Assert.True(migration.ChoiceAt >= beforeChoice);
        Assert.False(migration.IsPending);
    }

    [Fact]
    public void ChooseLinkToCatalog_WhenAlreadyChosen_ThrowsInvalidOperationException()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        migration.ChooseLinkToCatalog();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => migration.ChooseLinkToCatalog());
        Assert.Contains("already been made", exception.Message);
    }

    [Fact]
    public void ChooseKeepPrivate_WhenAlreadyChosen_ThrowsInvalidOperationException()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        migration.ChooseKeepPrivate();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => migration.ChooseKeepPrivate());
        Assert.Contains("already been made", exception.Message);
    }

    [Fact]
    public void ChooseKeepPrivate_AfterLinkToCatalog_ThrowsInvalidOperationException()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        migration.ChooseLinkToCatalog();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => migration.ChooseKeepPrivate());
        Assert.Contains("already been made", exception.Message);
    }
}
