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
        migration.Id.Should().NotBe(Guid.Empty);
        migration.ShareRequestId.Should().Be(shareRequestId);
        migration.PrivateGameId.Should().Be(privateGameId);
        migration.SharedGameId.Should().Be(sharedGameId);
        migration.UserId.Should().Be(userId);
        migration.Choice.Should().Be(PostApprovalMigrationChoice.Pending);
        migration.IsPending.Should().BeTrue();
        migration.ChoiceAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyShareRequestId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var act = () =>
            ProposalMigration.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("shareRequestId");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var act2 = () =>
            ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
        var exception = act2.Should().Throw<ArgumentException>().Which;

        exception.ParamName.Should().Be("userId");
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
        migration.Choice.Should().Be(PostApprovalMigrationChoice.LinkToCatalog);
        migration.ChoiceAt.Should().NotBeNull();
        (migration.ChoiceAt >= beforeChoice).Should().BeTrue();
        migration.IsPending.Should().BeFalse();
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
        migration.Choice.Should().Be(PostApprovalMigrationChoice.KeepPrivate);
        migration.ChoiceAt.Should().NotBeNull();
        (migration.ChoiceAt >= beforeChoice).Should().BeTrue();
        migration.IsPending.Should().BeFalse();
    }

    [Fact]
    public void ChooseLinkToCatalog_WhenAlreadyChosen_ThrowsInvalidOperationException()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        migration.ChooseLinkToCatalog();

        // Act & Assert
        var act3 = () => migration.ChooseLinkToCatalog();
        var exception = act3.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("already been made");
    }

    [Fact]
    public void ChooseKeepPrivate_WhenAlreadyChosen_ThrowsInvalidOperationException()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        migration.ChooseKeepPrivate();

        // Act & Assert
        var act4 = () => migration.ChooseKeepPrivate();
        var exception = act4.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("already been made");
    }

    [Fact]
    public void ChooseKeepPrivate_AfterLinkToCatalog_ThrowsInvalidOperationException()
    {
        // Arrange
        var migration = ProposalMigration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        migration.ChooseLinkToCatalog();

        // Act & Assert
        var act5 = () => migration.ChooseKeepPrivate();
        var exception = act5.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("already been made");
    }
}
