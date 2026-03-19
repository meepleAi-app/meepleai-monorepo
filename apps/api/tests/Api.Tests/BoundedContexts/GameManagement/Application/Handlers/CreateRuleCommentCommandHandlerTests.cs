using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for CreateRuleCommentCommandHandler.
/// Tests comment creation with @mention support and line number validation.
/// NOTE: Uses DbContext directly - simplified tests due to complex EF Core relationships.
/// ✅ RESOLVED: Integration tests added in CreateRuleCommentIntegrationTests.cs (Issue #1691)
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateRuleCommentCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
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
        var loggerMock = new Mock<ILogger<CreateRuleCommentCommandHandler>>();

        // Act
        var handler = new CreateRuleCommentCommandHandler(
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
        var loggerMock = new Mock<ILogger<CreateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateRuleCommentCommandHandler(
                null!,
                timeProviderMock.Object,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var loggerMock = new Mock<ILogger<CreateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CreateRuleCommentCommandHandler(
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
            new CreateRuleCommentCommandHandler(
                context,
                timeProviderMock.Object,
                null!));
    }
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
        Assert.Contains("@alice", command.CommentText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@bob", command.CommentText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Command_WithLongComment_ConstructsCorrectly()
    {
        // Act - Test with long comment (up to 2,000 character limit per database schema)
        var longText = new string('x', 2000);
        var command = new CreateRuleCommentCommand(
            GameId: Guid.NewGuid().ToString(),
            Version: "2.0",
            LineNumber: 1,
            CommentText: longText,
            UserId: Guid.NewGuid());

        // Assert
        Assert.Equal(2000, command.CommentText.Length);
    }
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
    // 8. Validation: Comment exceeding 2,000 characters throws exception
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

