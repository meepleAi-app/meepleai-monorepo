using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class GetTurnSummaryCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly GetTurnSummaryCommandHandler _handler;

    public GetTurnSummaryCommandHandlerTests()
    {
        _handler = new GetTurnSummaryCommandHandler(
            _sessionRepoMock.Object,
            _eventRepoMock.Object,
            _unitOfWorkMock.Object,
            _llmServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSummaryResult()
    {
        var sessionId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new GetTurnSummaryCommand(sessionId, requesterId, LastNEvents: 10);

        var session = Session.Create(requesterId, Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var events = new List<SessionEvent>
        {
            SessionEvent.Create(sessionId, "dice_roll", "{\"result\": 6}", requesterId, "player"),
            SessionEvent.Create(sessionId, "score_update", "{\"score\": 10}", requesterId, "player"),
        };

        _eventRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, null, 10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.AgentTask, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("A great game was had by all."));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Summary.Should().Be("A great game was had by all.");
        result.EventsAnalyzed.Should().Be(2);
        result.SummaryEventId.Should().NotBe(Guid.Empty);
        _eventRepoMock.Verify(r => r.AddAsync(It.IsAny<SessionEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var command = new GetTurnSummaryCommand(Guid.NewGuid(), Guid.NewGuid(), LastNEvents: 10);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoEvents_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new GetTurnSummaryCommand(sessionId, requesterId, LastNEvents: 10);

        var session = Session.Create(requesterId, Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _eventRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, null, 10, 0, It.IsAny<CancellationToken>())).ReturnsAsync(new List<SessionEvent>());

        var ex = (await ((Func<Task>)(() => _handler.Handle(command, CancellationToken.None))).Should().ThrowAsync<ConflictException>()).Which;
        ex.Message.Should().ContainEquivalentOf("No events found");
    }

    [Fact]
    public async Task Handle_LlmFailure_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new GetTurnSummaryCommand(sessionId, requesterId, LastNEvents: 10);

        var session = Session.Create(requesterId, Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var events = new List<SessionEvent>
        {
            SessionEvent.Create(sessionId, "dice_roll", "{\"result\": 3}", requesterId, "player"),
        };
        _eventRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, null, 10, 0, It.IsAny<CancellationToken>())).ReturnsAsync(events);
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), RequestSource.AgentTask, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Service unavailable"));

        var ex = (await ((Func<Task>)(() => _handler.Handle(command, CancellationToken.None))).Should().ThrowAsync<ConflictException>()).Which;
        ex.Message.Should().ContainEquivalentOf("AI summary generation failed");
    }

    [Fact]
    public async Task Handle_WithPhaseRange_FiltersEventsCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new GetTurnSummaryCommand(sessionId, requesterId, FromPhase: 1, ToPhase: 3);

        var session = Session.Create(requesterId, Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var events = new List<SessionEvent>
        {
            SessionEvent.Create(sessionId, "dice_roll", "{\"phase\": 1, \"result\": 4}", requesterId, "player"),
            SessionEvent.Create(sessionId, "dice_roll", "{\"phase\": 2, \"result\": 6}", requesterId, "player"),
            SessionEvent.Create(sessionId, "dice_roll", "{\"phase\": 5, \"result\": 2}", requesterId, "player"),
        };
        _eventRepoMock.Setup(r => r.GetBySessionIdAsync(sessionId, null, 500, 0, It.IsAny<CancellationToken>())).ReturnsAsync(events);
        _llmServiceMock
            .Setup(s => s.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), RequestSource.AgentTask, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Phases 1-3 summary."));

        var result = await _handler.Handle(command, CancellationToken.None);
        result.EventsAnalyzed.Should().Be(2);
    }

    [Fact]
    public void Constructor_NullDependencies_ThrowsArgumentNullException()
    {
        var act2 = () =>
            new GetTurnSummaryCommandHandler(null!, _eventRepoMock.Object, _unitOfWorkMock.Object, _llmServiceMock.Object);
        act2.Should().Throw<ArgumentNullException>();
        var act3 = () =>
            new GetTurnSummaryCommandHandler(_sessionRepoMock.Object, null!, _unitOfWorkMock.Object, _llmServiceMock.Object);
        act3.Should().Throw<ArgumentNullException>();
        var act4 = () =>
            new GetTurnSummaryCommandHandler(_sessionRepoMock.Object, _eventRepoMock.Object, null!, _llmServiceMock.Object);
        act4.Should().Throw<ArgumentNullException>();
        var act5 = () =>
            new GetTurnSummaryCommandHandler(_sessionRepoMock.Object, _eventRepoMock.Object, _unitOfWorkMock.Object, null!);
        act5.Should().Throw<ArgumentNullException>();
    }
}
