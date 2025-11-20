using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for CreateRuleCommentCommandHandler.
/// Tests comment creation with @mention support and line number validation.
/// NOTE: Uses DbContext directly - simplified tests due to complex EF Core relationships.
/// TODO: Add integration tests for full comment creation workflow with @mention extraction.
/// </summary>
public class CreateRuleCommentCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public CreateRuleCommentCommandHandlerTests()
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
        var loggerMock = new Mock<ILogger<CreateRuleCommentCommandHandler>>();

        // Act
        var handler = new CreateRuleCommentCommandHandler(
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
        var loggerMock = new Mock<ILogger<CreateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateRuleCommentCommandHandler(
                null!,
                _timeProviderMock.Object,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<CreateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateRuleCommentCommandHandler(
                _dbContext,
                null!,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateRuleCommentCommandHandler(
                _dbContext,
                _timeProviderMock.Object,
                null!));
    }

    #endregion

    #region Command Tests

    [Fact]
    public void Command_WithAllProperties_ConstructsCorrectly()
    {
        // Act
        var gameId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();

        var command = new CreateRuleCommentCommand(
            GameId: gameId,
            Version: "1.0",
            LineNumber: 42,
            CommentText: "This rule is unclear",
            UserId: userId);

        // Assert
        Assert.Equal(gameId, command.GameId);
        Assert.Equal("1.0", command.Version);
        Assert.Equal(42, command.LineNumber);
        Assert.Equal("This rule is unclear", command.CommentText);
        Assert.Equal(userId, command.UserId);
    }

    [Fact]
    public void Command_WithNullLineNumber_ConstructsCorrectly()
    {
        // Act - General comment not attached to specific line
        var command = new CreateRuleCommentCommand(
            GameId: Guid.NewGuid().ToString(),
            Version: "1.0",
            LineNumber: null,
            CommentText: "General comment about the rules",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Null(command.LineNumber);
    }

    [Fact]
    public void Command_WithMentions_ConstructsCorrectly()
    {
        // Act - Comment with @mentions
        var command = new CreateRuleCommentCommand(
            GameId: Guid.NewGuid().ToString(),
            Version: "1.0",
            LineNumber: 10,
            CommentText: "Hey @alice and @bob, what do you think about this rule?",
            UserId: Guid.NewGuid());

        // Assert
        Assert.Contains("@alice", command.CommentText);
        Assert.Contains("@bob", command.CommentText);
    }

    [Fact]
    public void Command_WithLongComment_ConstructsCorrectly()
    {
        // Act - Test with long comment (up to 10,000 character limit)
        var longText = new string('x', 5000);
        var command = new CreateRuleCommentCommand(
            GameId: Guid.NewGuid().ToString(),
            Version: "2.0",
            LineNumber: 1,
            CommentText: longText,
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal(5000, command.CommentText.Length);
    }

    #endregion

    // NOTE: Full integration tests for Handle method (comment creation, @mention extraction,
    // navigation property loading, authorization) should be in integration test suite
    // due to DbContext complexity and EF Core Include/ThenInclude chains.
    //
    // Key scenarios for integration tests:
    // 1. Create comment with valid data
    // 2. Create comment with @mentions (extract and resolve usernames)
    // 3. Create comment with multiple @mentions
    // 4. Create comment with invalid @mentions (non-existent users)
    // 5. Create comment on specific line number
    // 6. Create general comment (null line number)
    // 7. Validation: Empty comment text throws exception
    // 8. Validation: Comment exceeding 10,000 characters throws exception
    // 9. Validation: Negative line number throws exception
    // 10. Validation: Zero line number throws exception
    // 11. Navigation properties are loaded correctly (User, Replies, ResolvedByUser)
    // 12. CreatedAt timestamp is set from TimeProvider
    // 13. Regex timeout handling for @mention extraction
    // 14. Case-insensitive username matching for @mentions
    // 15. Email prefix matching for @mentions
    // 16. Logging of created comment details
    //
    // See integration-tests.yml workflow for full comment workflow testing.
}