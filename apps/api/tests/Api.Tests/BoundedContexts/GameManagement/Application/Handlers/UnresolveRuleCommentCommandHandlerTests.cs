using Microsoft.Extensions.Logging;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for UnresolveRuleCommentCommandHandler.
/// Tests comment unresolve with optional parent unresolve and authorization.
/// NOTE: Uses DbContext directly - simplified tests due to complex parent unresolve logic.
/// ✅ RESOLVED: Integration tests added for full unresolve workflow with parent cascading.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UnresolveRuleCommentCommandHandlerTests
{
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    private static Mock<TimeProvider> CreateTimeProviderMock()
    {
        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        return timeProviderMock;
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var timeProviderMock = CreateTimeProviderMock();
        var loggerMock = new Mock<ILogger<UnresolveRuleCommentCommandHandler>>();

        // Act
        var handler = new UnresolveRuleCommentCommandHandler(
            context,
            timeProviderMock.Object,
            loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        var timeProviderMock = CreateTimeProviderMock();
        var loggerMock = new Mock<ILogger<UnresolveRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UnresolveRuleCommentCommandHandler(
                null!,
                timeProviderMock.Object,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var loggerMock = new Mock<ILogger<UnresolveRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UnresolveRuleCommentCommandHandler(
                context,
                null!,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var timeProviderMock = CreateTimeProviderMock();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UnresolveRuleCommentCommandHandler(
                context,
                timeProviderMock.Object,
                null!));
    }
    [Fact]
    public void Command_AsOwner_ConstructsCorrectly()
    {
        // Act - Owner unresolving their own comment
        var commentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UnresolveRuleCommentCommand(
            CommentId: commentId,
            UserId: userId,
            IsAdmin: false,
            UnresolveParent: false);

        // Assert
        Assert.Equal(commentId, command.CommentId);
        Assert.Equal(userId, command.UserId);
        Assert.False(command.IsAdmin);
        Assert.False(command.UnresolveParent);
    }

    [Fact]
    public void Command_AsAdmin_ConstructsCorrectly()
    {
        // Act - Admin unresolving any comment
        var commentId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var command = new UnresolveRuleCommentCommand(
            CommentId: commentId,
            UserId: adminUserId,
            IsAdmin: true,
            UnresolveParent: false);

        // Assert
        Assert.Equal(commentId, command.CommentId);
        Assert.Equal(adminUserId, command.UserId);
        Assert.True(command.IsAdmin);
    }

    [Fact]
    public void Command_WithUnresolveParentTrue_ConstructsCorrectly()
    {
        // Act - Unresolve comment and parent
        var command = new UnresolveRuleCommentCommand(
            CommentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            IsAdmin: false,
            UnresolveParent: true);

        // Assert
        Assert.True(command.UnresolveParent);
    }

    [Fact]
    public void Command_WithDefaultParameters_UsesCorrectDefaults()
    {
        // Act - Use default parameters
        var commentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UnresolveRuleCommentCommand(
            CommentId: commentId,
            UserId: userId);

        // Assert - Both default to false
        Assert.False(command.IsAdmin);
        Assert.False(command.UnresolveParent);
    }

    [Fact]
    public void Command_AdminUnresolvingWithParent_ConstructsCorrectly()
    {
        // Act - Admin unresolving child and parent
        var command = new UnresolveRuleCommentCommand(
            CommentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            IsAdmin: true,
            UnresolveParent: true);

        // Assert
        Assert.True(command.IsAdmin);
        Assert.True(command.UnresolveParent);
    }

    [Fact]
    public void Command_TopLevelComment_ConstructsCorrectly()
    {
        // Act - Unresolve top-level comment (no parent to cascade to)
        var command = new UnresolveRuleCommentCommand(
            CommentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            IsAdmin: false,
            UnresolveParent: false); // No parent exists

        // Assert
        Assert.False(command.UnresolveParent);
    }

    [Fact]
    public void Command_WithDifferentUserIds_ConstructsCorrectly()
    {
        // Act - Different unresolvers for different comments
        var comment1Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var comment2Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var command1 = new UnresolveRuleCommentCommand(
            CommentId: comment1Id,
            UserId: user1Id);

        var command2 = new UnresolveRuleCommentCommand(
            CommentId: comment2Id,
            UserId: user2Id);

        // Assert
        Assert.NotEqual(command1.CommentId, command2.CommentId);
        Assert.NotEqual(command1.UserId, command2.UserId);
    }
    // NOTE: Full integration tests for Handle method (comment unresolve, parent unresolve cascading,
    // authorization, navigation property loading) should be in integration test suite
    // due to DbContext complexity and parent unresolve logic.
    //
    // Key scenarios for integration tests:
    // 1. Unresolve comment by owner succeeds
    // 2. Unresolve comment by non-owner non-admin throws UnauthorizedAccessException
    // 3. Unresolve comment by admin (not owner) succeeds
    // 4. Unresolve non-existent comment throws InvalidOperationException
    // 5. Unresolve comment without parent (UnresolveParent=false)
    // 6. Unresolve comment with parent (UnresolveParent=true) - cascades to parent
    // 7. Unresolve child when parent is already unresolved (no-op for parent)
    // 8. Unresolve child when parent is resolved (parent gets unresolved)
    // 9. IsResolved set to false, ResolvedByUserId and ResolvedAt cleared to null
    // 10. UpdatedAt timestamp is updated from TimeProvider
    // 11. Navigation properties are reloaded correctly after unresolve
    // 12. Logging of unresolved comment details (CommentId, UserId, UnresolveParent, IsAdmin)
    // 13. Logging when parent is unresolved due to child (ParentId and ChildId logged)
    // 14. Top-level comments (no parent) unresolve correctly with UnresolveParent=true
    // 15. Parent unresolve happens in same transaction as child unresolve
    //
    // See integration-tests.yml workflow for full comment unresolve workflow testing.
}

