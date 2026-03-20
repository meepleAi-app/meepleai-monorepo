using System.Runtime.CompilerServices;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for GenerateSetupChecklistCommandHandler.
/// TDD: Tests written first, then handler implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GenerateSetupChecklistCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GenerateSetupChecklistCommandHandler _handler;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultGameId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    public GenerateSetupChecklistCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _mediatorMock = new Mock<IMediator>();
        _featureFlagServiceMock = new Mock<IFeatureFlagService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new GenerateSetupChecklistCommandHandler(
            _repositoryMock.Object,
            _mediatorMock.Object,
            _featureFlagServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    // === Helpers ===

    private static LiveGameSession CreateSessionWithGameId(Guid? sessionId = null, Guid? gameId = null)
    {
        return LiveGameSession.Create(
            sessionId ?? DefaultSessionId,
            DefaultUserId,
            "Test Game",
            TimeProvider.System,
            gameId: gameId ?? DefaultGameId);
    }

    private static LiveGameSession CreateSessionWithoutGameId(Guid? sessionId = null)
    {
        return LiveGameSession.Create(
            sessionId ?? DefaultSessionId,
            DefaultUserId,
            "Test Game",
            TimeProvider.System,
            gameId: null);
    }

    private void SetupFeatureFlagEnabled(bool enabled = true)
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync("Features:SetupWizard.Enabled", null))
            .ReturnsAsync(enabled);
    }

    private void SetupRepoGetById(Guid sessionId, LiveGameSession? session)
    {
        _repositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupStreamingResponse(string gameId, int playerCount)
    {
        // Setup the mediator to return a streaming response with setup steps
        var steps = new List<SetupGuideStep>
        {
            new(1, "Prepare Board", "Place the board in the center", new List<Snippet>(), false),
            new(2, "Deal Cards", "Deal 7 cards to each player", new List<Snippet>(), false),
            new(3, "Place Tokens", "Each player takes their tokens", new List<Snippet>(), false)
        };

        var events = new List<RagStreamingEvent>
        {
            new(StreamingEventType.StateUpdate, new StreamingStateUpdate("Preparing..."), DateTime.UtcNow),
            new(StreamingEventType.SetupStep, new StreamingSetupStep(steps[0]), DateTime.UtcNow),
            new(StreamingEventType.SetupStep, new StreamingSetupStep(steps[1]), DateTime.UtcNow),
            new(StreamingEventType.SetupStep, new StreamingSetupStep(steps[2]), DateTime.UtcNow),
            new(StreamingEventType.Complete, new StreamingComplete(10, 100, 50, 150, 0.85), DateTime.UtcNow)
        };

        _mediatorMock
            .Setup(x => x.CreateStream(
                It.Is<StreamSetupGuideQuery>(q => q.GameId == gameId && q.PlayerCount == playerCount),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(events));
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    // === Feature flag disabled ===

    [Fact]
    public async Task Handle_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupFeatureFlagEnabled(false);
        var command = new GenerateSetupChecklistCommand(DefaultSessionId, 4);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Be("Feature SetupWizard.Enabled is disabled");
    }

    // === Session not found ===

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        SetupRepoGetById(DefaultSessionId, null);
        var command = new GenerateSetupChecklistCommand(DefaultSessionId, 4);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // === Session without GameId ===

    [Fact]
    public async Task Handle_SessionWithoutGameId_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        var session = CreateSessionWithoutGameId();
        SetupRepoGetById(DefaultSessionId, session);
        var command = new GenerateSetupChecklistCommand(DefaultSessionId, 4);

        // Act & Assert
        var act = 
            () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Be("Session has no associated game");
    }

    // === Valid flow ===

    [Fact]
    public async Task Handle_ValidSessionWithGameId_GeneratesChecklist()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        var session = CreateSessionWithGameId();
        SetupRepoGetById(DefaultSessionId, session);
        SetupStreamingResponse(DefaultGameId.ToString(), 4);
        var command = new GenerateSetupChecklistCommand(DefaultSessionId, 4);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PlayerCount.Should().Be(4);
        result.SetupSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidFlow_CallsSetSetupChecklistOnSession()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        var session = CreateSessionWithGameId();
        SetupRepoGetById(DefaultSessionId, session);
        SetupStreamingResponse(DefaultGameId.ToString(), 4);
        var command = new GenerateSetupChecklistCommand(DefaultSessionId, 4);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - checklist should be set on the session
        session.SetupChecklist.Should().NotBeNull();
        session.SetupChecklist!.PlayerCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ValidFlow_SendsStreamSetupGuideQueryWithCorrectParams()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        var gameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithGameId(sessionId, gameId);
        SetupRepoGetById(sessionId, session);
        SetupStreamingResponse(gameId.ToString(), 3);
        var command = new GenerateSetupChecklistCommand(sessionId, 3);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            x => x.CreateStream(
                It.Is<StreamSetupGuideQuery>(q =>
                    q.GameId == gameId.ToString() &&
                    q.PlayerCount == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFlow_ChecklistContainsStepsFromStream()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        var session = CreateSessionWithGameId();
        SetupRepoGetById(DefaultSessionId, session);
        SetupStreamingResponse(DefaultGameId.ToString(), 2);
        var command = new GenerateSetupChecklistCommand(DefaultSessionId, 2);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - should have 3 steps from the streaming response
        result.SetupSteps.Count.Should().Be(3);
        result.SetupSteps[0].Instruction.Should().Be("Place the board in the center");
        result.SetupSteps[1].Instruction.Should().Be("Deal 7 cards to each player");
        result.SetupSteps[2].Instruction.Should().Be("Each player takes their tokens");
    }
}
