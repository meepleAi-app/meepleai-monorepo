using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ProvideAgentFeedbackCommandHandler.
/// Tests feedback submission validation and storage.
/// </summary>
public class ProvideAgentFeedbackCommandHandlerTests
{
    private readonly Mock<ILogger<ProvideAgentFeedbackCommandHandler>> _mockLogger;
    private readonly TimeProvider _timeProvider;

    public ProvideAgentFeedbackCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<ProvideAgentFeedbackCommandHandler>>();
        _timeProvider = TimeProvider.System;
    }

    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return DbContextHelper.CreateInMemoryDbContext();
    }

    #region Validation Tests

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
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync();
        Assert.NotNull(feedback);
        Assert.Equal("helpful", feedback.Outcome);
    }

    [Fact]
    public async Task Handle_WithValidNotHelpfulOutcome_Succeeds()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync();
        Assert.NotNull(feedback);
        Assert.Equal("not-helpful", feedback.Outcome);
    }

    [Fact]
    public async Task Handle_WithValidIncorrectOutcome_Succeeds()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync();
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
        using var context = CreateFreshDbContext();
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
            () => handler.Handle(command, CancellationToken.None));

        Assert.Contains("Invalid outcome", exception.Message);
        Assert.Contains(invalidOutcome, exception.Message);
        Assert.Contains("helpful, not-helpful, incorrect", exception.Message);
    }

    [Fact]
    public async Task Handle_WithNullOutcome_RemovesExistingFeedback()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
        await context.SaveChangesAsync();

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/chat",
            UserId = userId.ToString(),
            Outcome = null  // Remove feedback
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync();
        Assert.Null(feedback);
    }

    [Fact]
    public async Task Handle_WithEmptyOutcome_RemovesExistingFeedback()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
        await context.SaveChangesAsync();

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/chat",
            UserId = userId.ToString(),
            Outcome = ""  // Empty string to remove feedback
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync();
        Assert.Null(feedback);
    }

    #endregion

    #region Required Field Validation Tests

    [Fact]
    public async Task Handle_WithNullMessageId_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptyMessageId_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNullEndpoint_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNullUserId_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
            () => handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Handle_WithExistingFeedback_UpdatesOutcome()
    {
        // Arrange
        using var context = CreateFreshDbContext();
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
        await context.SaveChangesAsync();

        var handler = new ProvideAgentFeedbackCommandHandler(context, _mockLogger.Object);
        var command = new ProvideAgentFeedbackCommand
        {
            MessageId = messageId.ToString(),
            Endpoint = "/api/v1/search",  // Different endpoint
            UserId = userId.ToString(),
            Outcome = "not-helpful"  // Different outcome
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var feedback = await context.AgentFeedbacks.FirstOrDefaultAsync();
        Assert.NotNull(feedback);
        Assert.Equal("not-helpful", feedback.Outcome);
        Assert.Equal("/api/v1/search", feedback.Endpoint);
    }

    #endregion
}
