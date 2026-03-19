using Api.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;

/// <summary>
/// Unit tests for HandleMigrationChoiceCommand.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class HandleMigrationChoiceCommandHandlerTests
{
    [Fact]
    public void HandleMigrationChoiceCommand_WithLinkToCatalog_CreatesWithCorrectProperties()
    {
        // Arrange
        var migrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command = new HandleMigrationChoiceCommand(
            MigrationId: migrationId,
            UserId: userId,
            Choice: PostApprovalMigrationChoice.LinkToCatalog
        );

        // Assert
        command.MigrationId.Should().Be(migrationId);
        command.UserId.Should().Be(userId);
        command.Choice.Should().Be(PostApprovalMigrationChoice.LinkToCatalog);
    }

    [Fact]
    public void HandleMigrationChoiceCommand_WithKeepPrivate_CreatesWithCorrectProperties()
    {
        // Arrange
        var migrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command = new HandleMigrationChoiceCommand(
            MigrationId: migrationId,
            UserId: userId,
            Choice: PostApprovalMigrationChoice.KeepPrivate
        );

        // Assert
        command.MigrationId.Should().Be(migrationId);
        command.UserId.Should().Be(userId);
        command.Choice.Should().Be(PostApprovalMigrationChoice.KeepPrivate);
    }
}
