using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Issue #3174 - the arbitro/decisore feedback handlers threw InvalidOperationException on an
/// invalid enum value supplied by the client, which the middleware maps to 500 instead of 400.
/// These tests assert the handlers surface BadRequestException (400) for invalid enum input.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class SubmitValidationFeedbackInvalidEnumTests
{
    private static GameSession ActiveSession() => new(
        id: Guid.NewGuid(),
        gameId: Guid.NewGuid(),
        players: new List<SessionPlayer> { new("Player 1", 1, "Red") },
        createdByUserId: Guid.NewGuid());

    [Fact]
    public async Task Handle_InvalidAccuracy_ThrowsBadRequestException()
    {
        var gameSessionId = Guid.NewGuid();
        var mockFeedbackRepo = new Mock<IArbitroValidationFeedbackRepository>();
        var mockSessionRepo = new Mock<IGameSessionRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<SubmitValidationFeedbackCommandHandler>>();

        mockSessionRepo
            .Setup(r => r.GetByIdAsync(gameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveSession());
        mockFeedbackRepo
            .Setup(r => r.GetByValidationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArbitroValidationFeedback?)null);

        var handler = new SubmitValidationFeedbackCommandHandler(
            mockFeedbackRepo.Object, mockSessionRepo.Object, mockUow.Object, mockLogger.Object, TimeProvider.System);

        var command = new SubmitValidationFeedbackCommand
        {
            ValidationId = Guid.NewGuid(),
            GameSessionId = gameSessionId,
            UserId = Guid.NewGuid(),
            Rating = 5,
            Accuracy = "not-a-valid-enum",
            AiDecision = "approved",
            AiConfidence = 0.9,
            HadConflicts = false,
        };

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*accuracy*");
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class SubmitDecisoreMoveFeedbackInvalidEnumTests
{
    private static GameSession ActiveSession() => new(
        id: Guid.NewGuid(),
        gameId: Guid.NewGuid(),
        players: new List<SessionPlayer> { new("Player 1", 1, "Red") },
        createdByUserId: Guid.NewGuid());

    private static (SubmitDecisoreMoveFeedbackCommandHandler handler, Guid gameSessionId) BuildHandler()
    {
        var gameSessionId = Guid.NewGuid();
        var mockFeedbackRepo = new Mock<IDecisoreMoveFeedbackRepository>();
        var mockSessionRepo = new Mock<IGameSessionRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<SubmitDecisoreMoveFeedbackCommandHandler>>();

        mockSessionRepo
            .Setup(r => r.GetByIdAsync(gameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveSession());
        mockFeedbackRepo
            .Setup(r => r.GetBySuggestionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DecisoreMoveFeedback?)null);

        var handler = new SubmitDecisoreMoveFeedbackCommandHandler(
            mockFeedbackRepo.Object, mockSessionRepo.Object, mockUow.Object, mockLogger.Object, TimeProvider.System);
        return (handler, gameSessionId);
    }

    private static SubmitDecisoreMoveFeedbackCommand Command(Guid gameSessionId, string quality, string outcome) => new()
    {
        SuggestionId = Guid.NewGuid(),
        GameSessionId = gameSessionId,
        UserId = Guid.NewGuid(),
        Rating = 4,
        Quality = quality,
        Outcome = outcome,
        SuggestionFollowed = true,
        TopSuggestedMove = "e4",
        PositionStrength = 0.5,
        AnalysisDepth = "Deep",
    };

    [Fact]
    public async Task Handle_InvalidQuality_ThrowsBadRequestException()
    {
        var (handler, gameSessionId) = BuildHandler();
        var command = Command(gameSessionId, quality: "not-a-valid-enum", outcome: "Win");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*quality*");
    }

    [Fact]
    public async Task Handle_InvalidOutcome_ThrowsBadRequestException()
    {
        var (handler, gameSessionId) = BuildHandler();
        var command = Command(gameSessionId, quality: "Helpful", outcome: "not-a-valid-enum");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*outcome*");
    }
}
