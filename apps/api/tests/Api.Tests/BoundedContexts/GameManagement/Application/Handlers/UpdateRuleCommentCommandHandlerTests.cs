using Microsoft.Extensions.Logging;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for UpdateRuleCommentCommandHandler.
/// Tests comment updates with ownership validation.
/// NOTE: Uses DbContext directly - simplified tests due to complex EF Core relationships.
/// ✅ RESOLVED: Integration tests added for full comment update workflow with authorization.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateRuleCommentCommandHandlerTests
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
        var loggerMock = new Mock<ILogger<UpdateRuleCommentCommandHandler>>();

        // Act
        var handler = new UpdateRuleCommentCommandHandler(
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
        var loggerMock = new Mock<ILogger<UpdateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleCommentCommandHandler(
                null!,
                timeProviderMock.Object,
                loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var loggerMock = new Mock<ILogger<UpdateRuleCommentCommandHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleCommentCommandHandler(
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
            new UpdateRuleCommentCommandHandler(
                context,
                timeProviderMock.Object,
                null!));
    }
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
        command.CommentId.Should().Be(commentId);
        command.CommentText.Should().Be("Updated comment text");
        command.UserId.Should().Be(userId);
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
        command.CommentText.Should().Be("OK");
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
        command.CommentText.Length.Should().Be(8000);
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
        command.CommentText.Should().Contain("🎲");
        command.CommentText.Should().Contain("émojis");
    }
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

