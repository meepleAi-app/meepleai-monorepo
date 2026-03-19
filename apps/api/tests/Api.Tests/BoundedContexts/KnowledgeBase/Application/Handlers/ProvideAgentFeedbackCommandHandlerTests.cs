using System.Threading;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ProvideAgentFeedbackCommandHandler.
/// Tests feedback submission validation and storage.
/// Issue #3352: Added tests for Comment field support.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ProvideAgentFeedbackCommandHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<ILogger<ProvideAgentFeedbackCommandHandler>> _mockLogger;

    public ProvideAgentFeedbackCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<ProvideAgentFeedbackCommandHandler>>();
    }

    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }
    [Fact]
    public async Task Handle_WithValidHelpfulOutcome_Succeeds()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "helpful",
            GameId = Guid.NewGuid().ToString()
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("helpful", feedback.Outcome);
    }

    [Fact]
    public async Task Handle_WithValidNotHelpfulOutcome_Succeeds()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "not-helpful",
            GameId = Guid.NewGuid().ToString()
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("not-helpful", feedback.Outcome);
    }

    [Fact]
    public async Task Handle_WithValidIncorrectOutcome_Succeeds()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "incorrect",
            GameId = Guid.NewGuid().ToString()
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("incorrect", feedback.Outcome);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not_helpful")]  // Underscore instead of hyphen
    [InlineData("nothelpful")]
    [InlineData("unhelpful")]
    [InlineData("HELPFUL")]      // Wrong case
    [InlineData("Not-Helpful")]  // Wrong case
    public async Task Handle_WithInvalidOutcome_ThrowsArgumentException(string invalidOutcome)
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = invalidOutcome,
            GameId = Guid.NewGuid().ToString()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(command, TestCancellationToken));

        Assert.Contains("Invalid outcome", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(invalidOutcome, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("helpful, not-helpful, incorrect", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithNullOutcome_RemovesExistingFeedback()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Add existing feedback
        context.AgentFeedbacks.Add(new AgentFeedbackEntity
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            Endpoint = "/api/v1/chat",
            UserId = userId,
            Outcome = "helpful",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/chat",
            UserId = userId.ToString(),
            Outcome = null  // Remove feedback
        };

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.Null(feedback);
    }

    [Fact]
    public async Task Handle_WithEmptyOutcome_RemovesExistingFeedback()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Add existing feedback
        context.AgentFeedbacks.Add(new AgentFeedbackEntity
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            Endpoint = "/api/v1/chat",
            UserId = userId,
            Outcome = "helpful",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/chat",
            UserId = userId.ToString(),
            Outcome = ""  // Empty string to remove feedback
        };

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.Null(feedback);
    }
    [Fact]
    public async Task Handle_WithNullMessageId_ThrowsArgumentException()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = null!,
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "helpful"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithEmptyMessageId_ThrowsArgumentException()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = "",
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "helpful"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNullEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = null!,
            UserId = Guid.NewGuid().ToString(),
            Outcome = "helpful"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNullUserId_ThrowsArgumentException()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = null!,
            Outcome = "helpful"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Handle_WithExistingFeedback_UpdatesOutcome()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Add existing feedback
        context.AgentFeedbacks.Add(new AgentFeedbackEntity
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            Endpoint = "/api/v1/chat",
            UserId = userId,
            Outcome = "helpful",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/search",  // Different endpoint
            UserId = userId.ToString(),
            Outcome = "not-helpful"  // Different outcome
        };

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("not-helpful", feedback.Outcome);
        Assert.Equal("/api/v1/search", feedback.Endpoint);
    }

    // Issue #3352: Tests for Comment field support

    [Fact]
    public async Task Handle_WithComment_StoresComment()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "not-helpful",
            GameId = Guid.NewGuid().ToString(),
            Comment = "The response was unclear about game setup rules"
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("not-helpful", feedback.Outcome);
        Assert.Equal("The response was unclear about game setup rules", feedback.Comment);
    }

    [Fact]
    public async Task Handle_WithNullComment_StoresNullComment()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "helpful",
            GameId = Guid.NewGuid().ToString(),
            Comment = null
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("helpful", feedback.Outcome);
        Assert.Null(feedback.Comment);
    }

    [Fact]
    public async Task Handle_WithEmptyComment_StoresNullComment()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "not-helpful",
            Comment = "   "  // Whitespace only should be trimmed to null
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Null(feedback.Comment);
    }

    [Fact]
    public async Task Handle_WithCommentUpdate_UpdatesComment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Add existing feedback without comment
        context.AgentFeedbacks.Add(new AgentFeedbackEntity
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            Endpoint = "/api/v1/chat",
            UserId = userId,
            Outcome = "not-helpful",
            Comment = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/chat",
            UserId = userId.ToString(),
            Outcome = "not-helpful",
            Comment = "Adding a comment to existing feedback"
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("Adding a comment to existing feedback", feedback.Comment);
    }

    [Fact]
    public async Task Handle_WithCommentOnPositiveFeedback_StoresComment()
    {
        // Arrange - Comments should work for any outcome type
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "helpful",
            Comment = "Great explanation of the rules!"
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("helpful", feedback.Outcome);
        Assert.Equal("Great explanation of the rules!", feedback.Comment);
    }

    [Fact]
    public async Task Handle_WithCommentTrim_TrimsWhitespace()
    {
        // Arrange
        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = Guid.NewGuid().ToString(),
            Endpoint = "/api/v1/chat",
            UserId = Guid.NewGuid().ToString(),
            Outcome = "not-helpful",
            Comment = "  Comment with extra spaces  "
        };

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync(TestCancellationToken);
        Assert.NotNull(feedback);
        Assert.Equal("Comment with extra spaces", feedback.Comment);
    }
}

