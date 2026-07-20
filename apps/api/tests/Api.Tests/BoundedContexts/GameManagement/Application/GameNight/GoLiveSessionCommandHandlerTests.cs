using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for <see cref="GoLiveSessionCommandHandler"/> (epic #3188 Slice 2).
/// Validates the promote-then-open-live phases and the 404/409 error mapping, mirroring the
/// mocking style of <c>StartGameNightSessionCommandHandlerTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GoLiveSessionCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly GoLiveSessionCommandHandler _handler;

    public GoLiveSessionCommandHandlerTests()
    {
        _mockRepository = new Mock<IGameNightEventRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new GoLiveSessionCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object);
    }

    private static GameNightEvent CreatePublishedEventWithDraft(Guid sessionId, out Guid gameId)
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        gameId = evt.GameIds[0];
        evt.AddSession(sessionId, gameId, "Catan"); // Pending draft
        return evt;
    }

    [Fact]
    public async Task Handle_HappyPath_PromotesDraftAndOpensLiveMode()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameNight = CreatePublishedEventWithDraft(sessionId, out _);

        _mockRepository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockMediator.Setup(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new GoLiveSessionCommand(sessionId, gameNight.OrganizerId), CancellationToken.None);

        // Assert — the targeted draft is InProgress and the result carries the promoted state
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(gameNight.Id, result.GameNightId);
        Assert.Equal(nameof(GameNightSessionStatus.InProgress), result.Status);
        Assert.Equal(1, result.PlayOrder);
        Assert.Equal(GameNightSessionStatus.InProgress, gameNight.Sessions[0].Status);

        // Persisted then opened live LAST (phase 3), exactly once each.
        _mockRepository.Verify(r => r.UpdateAsync(gameNight, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Send(
            It.Is<OpenSessionLiveModeCommand>(c => c.SessionId == sessionId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OwningNightNotFound_ThrowsNotFound_DoesNotOpenLiveMode()
    {
        // Arrange — no game night owns this session id.
        var sessionId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(new GoLiveSessionCommand(sessionId, Guid.NewGuid()), CancellationToken.None));

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonOrganizer_ThrowsForbidden()
    {
        // Arrange — a caller who is NOT the night's organizer must be rejected (parity with
        // StartGameNightSessionCommandHandler), before any promotion or live-mode dispatch.
        var sessionId = Guid.NewGuid();
        var gameNight = CreatePublishedEventWithDraft(sessionId, out _);
        var nonOrganizer = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(new GoLiveSessionCommand(sessionId, nonOrganizer), CancellationToken.None));

        // The blocked caller must not promote the draft or open live mode.
        Assert.Equal(GameNightSessionStatus.Pending, gameNight.Sessions[0].Status);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent.GameNightEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AnotherSessionAlreadyLive_Throws409MaxLive_DoesNotOpenLiveMode()
    {
        // Arrange — a first draft is already InProgress; going live on a second draft is blocked by
        // the in-memory max-1-live guard (invariante #10) BEFORE any DB write.
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        var gameNight = CreatePublishedEventWithDraft(firstSessionId, out _);
        gameNight.AddSession(secondSessionId, gameNight.GameIds[1], "Dixit");
        gameNight.StartSession(firstSessionId); // first draft now live

        _mockRepository.Setup(r => r.GetByLinkedSessionIdAsync(secondSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        // Act & Assert
        await Assert.ThrowsAsync<MaxLiveSessionsExceededException>(
            () => _handler.Handle(new GoLiveSessionCommand(secondSessionId, gameNight.OrganizerId), CancellationToken.None));

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TargetedSessionNotPending_ThrowsConflict()
    {
        // Arrange — the targeted session was already completed; a non-Pending promotion is a conflict
        // (InvalidOperationException from GameNightSession.Start() → ConflictException 409).
        var sessionId = Guid.NewGuid();
        var gameNight = CreatePublishedEventWithDraft(sessionId, out _);
        gameNight.StartSession(sessionId);
        gameNight.CompleteCurrentSession(winnerId: null); // now Completed

        _mockRepository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(new GoLiveSessionCommand(sessionId, gameNight.OrganizerId), CancellationToken.None));

        _mockMediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrentXminLoser_Maps409MaxLive_DoesNotOpenLiveMode()
    {
        // Arrange — two concurrent go-lives both pass the in-memory guard; the xmin loser's aggregate
        // SaveChanges throws DbUpdateConcurrencyException. It must map to the blocked-modal 409, and
        // MUST NOT open live mode on a promotion that never committed.
        var sessionId = Guid.NewGuid();
        var gameNight = CreatePublishedEventWithDraft(sessionId, out _);

        _mockRepository.Setup(r => r.GetByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        // Act & Assert
        await Assert.ThrowsAsync<MaxLiveSessionsExceededException>(
            () => _handler.Handle(new GoLiveSessionCommand(sessionId, gameNight.OrganizerId), CancellationToken.None));

        _mockMediator.Verify(m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
