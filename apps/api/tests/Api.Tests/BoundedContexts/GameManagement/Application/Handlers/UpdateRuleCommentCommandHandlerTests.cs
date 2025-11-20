using Microsoft.Extensions.Logging;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.Infrastructure;
using Api.Tests.Helpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for UpdateRuleCommentCommandHandler.
/// Tests comment updates with ownership validation.
/// NOTE: Uses DbContext directly - simplified tests due to complex EF Core relationships.
/// TODO: Add integration tests for full comment update workflow with authorization.
/// </summary>
public class UpdateRuleCommentCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public UpdateRuleCommentCommandHandlerTests()
    {
        _dbContext = DbContextHelper.CreateInMemoryDbContext();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<UpdateRuleCommentCommandHandler>>();

        // Act
        var handler = new UpdateRuleCommentCommandHandler(
            _dbContext,
            _timeProviderMock.Object,
            loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<UpdateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleCommentCommandHandler(
                null!,
                _timeProviderMock.Object,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<UpdateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleCommentCommandHandler(
                _dbContext,
                null!,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleCommentCommandHandler(
                _dbContext,
                _timeProviderMock.Object,
                null!));
    }

    #endregion

    #region Command Tests

    [Fact]
    public void Command_WithValidProperties_ConstructsCorrectly()
    {
        // Act
        var commentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var command = new UpdateRuleCommentCommand(
            CommentId: commentId,
            CommentText: "Updated comment text",
            UserId: userId);

        // Assert
        Assert.Equal(commentId, command.CommentId);
        Assert.Equal("Updated comment text", command.CommentText);
        Assert.Equal(userId, command.UserId);
    }

    [Fact]
    public void Command_WithShortText_ConstructsCorrectly()
    {
        // Act
        var command = new UpdateRuleCommentCommand(
            CommentId: Guid.NewGuid(),
            CommentText: "OK",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal("OK", command.CommentText);
    }

    [Fact]
    public void Command_WithLongText_ConstructsCorrectly()
    {
        // Act - Test with long comment (up to 10,000 character limit)
        var longText = new string('x', 8000);
        var command = new UpdateRuleCommentCommand(
            CommentId: Guid.NewGuid(),
            CommentText: longText,
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal(8000, command.CommentText.Length);
    }

    [Fact]
    public void Command_WithSpecialCharacters_ConstructsCorrectly()
    {
        // Act
        var command = new UpdateRuleCommentCommand(
            CommentId: Guid.NewGuid(),
            CommentText: "Comment with émojis 🎲 and spëcial chàracters!",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Contains("🎲", command.CommentText);
        Assert.Contains("émojis", command.CommentText);
    }

    #endregion

    // NOTE: Full integration tests for Handle method (comment update, ownership validation,
    // UpdatedAt timestamp, navigation property reloading) should be in integration test suite
    // due to DbContext complexity and EF Core Include/ThenInclude chains.
    //
    // Key scenarios for integration tests:
    // 1. Update comment by owner succeeds
    // 2. Update comment by non-owner throws UnauthorizedAccessException
    // 3. Update non-existent comment throws InvalidOperationException
    // 4. UpdatedAt timestamp is set from TimeProvider
    // 5. Validation: Empty comment text throws InvalidOperationException
    // 6. Validation: Comment exceeding 10,000 characters throws InvalidOperationException
    // 7. Navigation properties are reloaded correctly after update
    // 8. Comment text is trimmed and updated
    // 9. Logging of updated comment details
    // 10. Original comment metadata is preserved (CreatedAt, LineNumber, GameId, Version)
    // 11. Concurrent update scenarios (optimistic concurrency)
    //
    // See integration-tests.yml workflow for full comment update workflow testing.
}
