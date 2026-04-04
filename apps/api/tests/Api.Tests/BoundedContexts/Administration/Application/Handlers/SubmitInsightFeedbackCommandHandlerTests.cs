using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.SharedKernel.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Integration tests for SubmitInsightFeedbackCommandHandler.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class SubmitInsightFeedbackCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SubmitInsightFeedbackCommandHandler _handler;

    public SubmitInsightFeedbackCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"InsightFeedback_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 16, 12, 0, 0, TimeSpan.Zero));
        _handler = new SubmitInsightFeedbackCommandHandler(
            _dbContext,
            NullLogger<SubmitInsightFeedbackCommandHandler>.Instance,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsEntity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new SubmitInsightFeedbackCommand
        {
            UserId = userId,
            InsightId = "backlog-user-20260216",
            InsightType = "Backlog",
            IsRelevant = true,
            Comment = "Very helpful insight"
        };

        // Act
        var feedbackId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        feedbackId.Should().NotBe(Guid.Empty);

        var entity = await _dbContext.Set<InsightFeedbackEntity>()
            .FirstOrDefaultAsync(f => f.Id == feedbackId);

        entity.Should().NotBeNull();
        entity.UserId.Should().Be(userId);
        entity.InsightId.Should().Be("backlog-user-20260216");
        entity.InsightType.Should().Be("Backlog");
        entity.IsRelevant.Should().BeTrue();
        entity.Comment.Should().Be("Very helpful insight");
        entity.SubmittedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Handle_WithNullComment_PersistsWithNullComment()
    {
        // Arrange
        var command = new SubmitInsightFeedbackCommand
        {
            UserId = Guid.NewGuid(),
            InsightId = "streak-user-20260216",
            InsightType = "Streak",
            IsRelevant = false,
            Comment = null
        };

        // Act
        var feedbackId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var entity = await _dbContext.Set<InsightFeedbackEntity>()
            .FirstOrDefaultAsync(f => f.Id == feedbackId);

        entity.Should().NotBeNull();
        entity.Comment.Should().BeNull();
        entity.IsRelevant.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TrimsCommentWhitespace()
    {
        // Arrange
        var command = new SubmitInsightFeedbackCommand
        {
            UserId = Guid.NewGuid(),
            InsightId = "recommendation-user-20260216",
            InsightType = "Recommendation",
            IsRelevant = true,
            Comment = "  Good recommendation  "
        };

        // Act
        var feedbackId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var entity = await _dbContext.Set<InsightFeedbackEntity>()
            .FirstOrDefaultAsync(f => f.Id == feedbackId);

        entity.Should().NotBeNull();
        entity.Comment.Should().Be("Good recommendation");
    }

    [Fact]
    public async Task Handle_MultipleFeedbacks_PersistsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var commands = new[]
        {
            new SubmitInsightFeedbackCommand
            {
                UserId = userId,
                InsightId = "backlog-1",
                InsightType = "Backlog",
                IsRelevant = true
            },
            new SubmitInsightFeedbackCommand
            {
                UserId = userId,
                InsightId = "streak-1",
                InsightType = "Streak",
                IsRelevant = false
            },
            new SubmitInsightFeedbackCommand
            {
                UserId = userId,
                InsightId = "recommendation-1",
                InsightType = "Recommendation",
                IsRelevant = true
            }
        };

        // Act
        foreach (var cmd in commands)
        {
            await _handler.Handle(cmd, CancellationToken.None);
        }

        // Assert
        var feedbacks = await _dbContext.Set<InsightFeedbackEntity>()
            .Where(f => f.UserId == userId)
            .ToListAsync();

        feedbacks.Count.Should().Be(3);
        feedbacks.Count(f => f.IsRelevant).Should().Be(2);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_UsesTimeProviderForTimestamp()
    {
        // Arrange
        var specificTime = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(specificTime);

        var command = new SubmitInsightFeedbackCommand
        {
            UserId = Guid.NewGuid(),
            InsightId = "achievement-1",
            InsightType = "Achievement",
            IsRelevant = true
        };

        // Act
        var feedbackId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var entity = await _dbContext.Set<InsightFeedbackEntity>()
            .FirstOrDefaultAsync(f => f.Id == feedbackId);

        entity.Should().NotBeNull();
        entity.SubmittedAt.Should().Be(specificTime.UtcDateTime);
    }

    [Fact]
    public async Task Handle_DuplicateFeedback_ThrowsConflictException()
    {
        // Arrange: Submit feedback once
        var userId = Guid.NewGuid();
        var insightId = "backlog-duplicate-test";
        var command = new SubmitInsightFeedbackCommand
        {
            UserId = userId,
            InsightId = insightId,
            InsightType = "Backlog",
            IsRelevant = true
        };

        await _handler.Handle(command, CancellationToken.None);

        // Act & Assert: Submit same feedback again should throw ConflictException
        var duplicateCommand = new SubmitInsightFeedbackCommand
        {
            UserId = userId,
            InsightId = insightId,
            InsightType = "Backlog",
            IsRelevant = false
        };

        var act = () => _handler.Handle(duplicateCommand, CancellationToken.None);
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ConflictException>();
    }

    [Fact]
    public async Task Handle_SameInsightDifferentUser_Succeeds()
    {
        // Arrange: Two different users, same insight
        var insightId = "backlog-shared-insight";
        var command1 = new SubmitInsightFeedbackCommand
        {
            UserId = Guid.NewGuid(),
            InsightId = insightId,
            InsightType = "Backlog",
            IsRelevant = true
        };
        var command2 = new SubmitInsightFeedbackCommand
        {
            UserId = Guid.NewGuid(),
            InsightId = insightId,
            InsightType = "Backlog",
            IsRelevant = false
        };

        // Act: Both should succeed
        var id1 = await _handler.Handle(command1, CancellationToken.None);
        var id2 = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        id1.Should().NotBe(Guid.Empty);
        id2.Should().NotBe(Guid.Empty);
        id2.Should().NotBe(id1);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
