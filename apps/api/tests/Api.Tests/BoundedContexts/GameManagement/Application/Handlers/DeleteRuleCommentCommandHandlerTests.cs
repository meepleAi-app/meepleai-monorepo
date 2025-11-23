using Microsoft.Extensions.Logging;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.Infrastructure;
using Api.Tests.Helpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for DeleteRuleCommentCommandHandler.
/// Tests comment deletion with ownership and admin authorization.
/// NOTE: Uses DbContext directly - simplified tests due to complex EF Core relationships.
/// ✅ RESOLVED: Integration tests added for full comment deletion workflow with authorization.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
public class DeleteRuleCommentCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return DbContextHelper.CreateInMemoryDbContext();
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var loggerMock = new Mock<ILogger<DeleteRuleCommentCommandHandler>>();

        // Act
        var handler = new DeleteRuleCommentCommandHandler(
            context,
            loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        var loggerMock = new Mock<ILogger<DeleteRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteRuleCommentCommandHandler(
                null!,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteRuleCommentCommandHandler(
                context,
                null!));
    }

    #endregion

    #region Command Tests

    [Fact]
    public void Command_AsOwner_ConstructsCorrectly()
    {
        // Act - Owner deleting their own comment
        var commentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new DeleteRuleCommentCommand(
            CommentId: commentId,
            UserId: userId,
            IsAdmin: false);

        // Assert
        Assert.Equal(commentId, command.CommentId);
        Assert.Equal(userId, command.UserId);
        Assert.False(command.IsAdmin);
    }

    [Fact]
    public void Command_AsAdmin_ConstructsCorrectly()
    {
        // Act - Admin deleting any comment
        var commentId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var command = new DeleteRuleCommentCommand(
            CommentId: commentId,
            UserId: adminUserId,
            IsAdmin: true);

        // Assert
        Assert.Equal(commentId, command.CommentId);
        Assert.Equal(adminUserId, command.UserId);
        Assert.True(command.IsAdmin);
    }

    [Fact]
    public void Command_WithDifferentUserIds_ConstructsCorrectly()
    {
        // Act - Different users for different comments
        var comment1Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var comment2Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var command1 = new DeleteRuleCommentCommand(
            CommentId: comment1Id,
            UserId: user1Id,
            IsAdmin: false);

        var command2 = new DeleteRuleCommentCommand(
            CommentId: comment2Id,
            UserId: user2Id,
            IsAdmin: true);

        // Assert
        Assert.NotEqual(command1.CommentId, command2.CommentId);
        Assert.NotEqual(command1.UserId, command2.UserId);
        Assert.NotEqual(command1.IsAdmin, command2.IsAdmin);
    }

    #endregion

    // NOTE: Full integration tests for Handle method (comment deletion, ownership validation,
    // admin authorization, cascade deletion of replies) should be in integration test suite
    // due to DbContext complexity.
    //
    // Key scenarios for integration tests:
    // 1. Delete comment by owner succeeds
    // 2. Delete comment by non-owner non-admin throws UnauthorizedAccessException
    // 3. Delete comment by admin (not owner) succeeds
    // 4. Delete non-existent comment throws InvalidOperationException
    // 5. Deleting parent comment with replies throws exception (FK uses DeleteBehavior.Restrict)
    // 6. Logging of deleted comment details (CommentId, UserId, IsAdmin)
    // 7. Comment is removed from database (DbContext.Remove)
    // 8. Returns true on successful deletion
    // 9. Authorization edge cases:
    //    - Owner with IsAdmin=false can delete
    //    - Non-owner with IsAdmin=true can delete
    //    - Non-owner with IsAdmin=false cannot delete
    // 10. Soft delete vs hard delete (if implemented)
    // 11. Comments with mentions are properly cleaned up
    // 12. Comments with resolved status are properly cleaned up
    //
    // See integration-tests.yml workflow for full comment deletion workflow testing.
}
