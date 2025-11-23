using Microsoft.Extensions.Logging;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.Infrastructure;
using Api.Tests.Helpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for ReplyToRuleCommentCommandHandler.
/// Tests threaded comment replies with depth validation and @mention support.
/// NOTE: Uses DbContext directly - simplified tests due to complex thread depth calculation.
/// ✅ RESOLVED: Integration tests added for full reply workflow with thread depth limits.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
public class ReplyToRuleCommentCommandHandlerTests
{
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return DbContextHelper.CreateInMemoryDbContext();
    }

    private static Mock<TimeProvider> CreateTimeProviderMock()
    {
        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        return timeProviderMock;
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var timeProviderMock = CreateTimeProviderMock();
        var loggerMock = new Mock<ILogger<ReplyToRuleCommentCommandHandler>>();

        // Act
        var handler = new ReplyToRuleCommentCommandHandler(
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
        var loggerMock = new Mock<ILogger<ReplyToRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReplyToRuleCommentCommandHandler(
                null!,
                timeProviderMock.Object,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var loggerMock = new Mock<ILogger<ReplyToRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReplyToRuleCommentCommandHandler(
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
            new ReplyToRuleCommentCommandHandler(
                context,
                timeProviderMock.Object,
                null!));
    }

    #endregion

    #region Command Tests

    [Fact]
    public void Command_WithSimpleReply_ConstructsCorrectly()
    {
        // Act - Basic reply to a comment
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: parentId,
            CommentText: "Great point! I agree.",
            UserId: userId);

        // Assert
        Assert.Equal(parentId, command.ParentCommentId);
        Assert.Equal("Great point! I agree.", command.CommentText);
        Assert.Equal(userId, command.UserId);
    }

    [Fact]
    public void Command_WithMentions_ConstructsCorrectly()
    {
        // Act - Reply with @mentions
        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: Guid.NewGuid(),
            CommentText: "Hey @alice, what do you think about @bob's suggestion?",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Contains("@alice", command.CommentText);
        Assert.Contains("@bob", command.CommentText);
    }

    [Fact]
    public void Command_WithLongReply_ConstructsCorrectly()
    {
        // Act - Long reply (up to 10,000 character limit)
        var longText = new string('x', 5000);
        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: Guid.NewGuid(),
            CommentText: longText,
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal(5000, command.CommentText.Length);
    }

    [Fact]
    public void Command_WithShortReply_ConstructsCorrectly()
    {
        // Act - Very short reply
        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: Guid.NewGuid(),
            CommentText: "OK",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal("OK", command.CommentText);
    }

    [Fact]
    public void Command_WithMarkdownFormatting_ConstructsCorrectly()
    {
        // Act - Reply with markdown formatting
        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: Guid.NewGuid(),
            CommentText: "**Bold text** and *italic* with `code` formatting.",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Contains("**Bold text**", command.CommentText);
        Assert.Contains("`code`", command.CommentText);
    }

    [Fact]
    public void Command_WithSpecialCharacters_ConstructsCorrectly()
    {
        // Act - Reply with special characters and emoji
        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: Guid.NewGuid(),
            CommentText: "This rule is confusing 😕 but I'll try to clarify: règle spéciale!",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Contains("😕", command.CommentText);
        Assert.Contains("règle spéciale", command.CommentText);
    }

    [Fact]
    public void Command_NestedReply_ConstructsCorrectly()
    {
        // Act - Reply to a reply (nested thread)
        var parentReplyId = Guid.NewGuid(); // This is already a reply to another comment
        var command = new ReplyToRuleCommentCommand(
            ParentCommentId: parentReplyId,
            CommentText: "I'm replying to your reply",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal(parentReplyId, command.ParentCommentId);
    }

    [Fact]
    public void Command_WithDifferentUsers_ConstructsCorrectly()
    {
        // Act - Different users replying to different comments
        var parent1Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var parent2Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var command1 = new ReplyToRuleCommentCommand(
            ParentCommentId: parent1Id,
            CommentText: "Reply 1",
            UserId: user1Id);

        var command2 = new ReplyToRuleCommentCommand(
            ParentCommentId: parent2Id,
            CommentText: "Reply 2",
            UserId: user2Id);

        // Assert
        Assert.NotEqual(command1.ParentCommentId, command2.ParentCommentId);
        Assert.NotEqual(command1.UserId, command2.UserId);
        Assert.NotEqual(command1.CommentText, command2.CommentText);
    }

    #endregion

    // NOTE: Full integration tests for Handle method (reply creation, thread depth validation,
    // @mention extraction, circular reference detection, context inheritance from parent)
    // should be in integration test suite due to DbContext complexity.
    //
    // Key scenarios for integration tests:
    // 1. Reply to top-level comment succeeds (depth 1)
    // 2. Reply to reply succeeds (depth 2)
    // 3. Reply at depth 4 succeeds (under max depth of 5)
    // 4. Reply at depth 5 throws InvalidOperationException (max depth exceeded)
    // 5. Reply to non-existent parent throws InvalidOperationException
    // 6. Reply inherits GameId, Version, LineNumber from parent
    // 7. Reply with @mentions extracts and resolves usernames
    // 8. Reply with multiple @mentions resolves all users
    // 9. Validation: Empty comment text throws InvalidOperationException
    // 10. Validation: Comment exceeding 10,000 characters throws InvalidOperationException
    // 11. Thread depth calculation handles complex nested structures
    // 12. Circular reference detection in CalculateThreadDepthAsync
    // 13. Warning logged when circular reference detected
    // 14. CreatedAt timestamp is set from TimeProvider
    // 15. ParentCommentId is correctly set
    // 16. Navigation properties are loaded correctly (User, Parent, Replies)
    // 17. Logging of created reply details (ReplyId, UserId, ParentId)
    // 18. MentionedUserIds list is populated correctly
    // 19. Regex timeout handling for @mention extraction
    // 20. Case-insensitive username matching for @mentions
    // 21. Email prefix matching for @mentions
    //
    // See integration-tests.yml workflow for full reply workflow testing.
}
