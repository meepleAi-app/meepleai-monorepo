using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for StartGameNightSessionCommandHandler.
/// Validates cross-BC session creation via MediatR and game night aggregate linking.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IAutoSaveSchedulerService> _mockAutoSaveScheduler;
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionCommandHandlerTests()
    {
        _mockRepository = new Mock<IGameNightEventRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockAutoSaveScheduler = new Mock<IAutoSaveSchedulerService>();
        _handler = new StartGameNightSessionCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockAutoSaveScheduler.Object);
    }

    private static GameNightEvent CreatePublishedEvent(Guid? organizerId = null)
    {
        var orgId = organizerId ?? Guid.NewGuid();
        var evt = GameNightEvent.Create(
            orgId, "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        return evt;
    }

    [Fact]
    public async Task Handle_CreatesSessionAndLinksToGameNight()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];
        var newSessionId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(newSessionId, "ABC123", []));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(newSessionId, result.SessionId);
        Assert.Equal("ABC123", result.SessionCode);
        Assert.Equal(1, result.PlayOrder);
        Assert.Single(gameNight.Sessions);

        _mockRepository.Verify(r => r.UpdateAsync(gameNight, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOrganizer_ThrowsForbidden()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var differentUserId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var command = new StartGameNightSessionCommand(
            gameNight.Id, Guid.NewGuid(), "Catan", differentUserId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));

        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_GameNightNotFound_ThrowsNotFound()
    {
        // Arrange
        var gameNightId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var command = new StartGameNightSessionCommand(
            gameNightId, Guid.NewGuid(), "Catan", Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DispatchesCreateSessionCommandWithCorrectParameters()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "XYZ789", []));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.UserId == gameNight.OrganizerId &&
                c.GameId == gameId &&
                c.SessionType == "GameSpecific" &&
                c.Participants.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionStartedAfterAdding()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var sessionId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(sessionId, "DEF456", []));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameNight.GameIds[0], "Catan", gameNight.OrganizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — session should be in InProgress status after Start
        var gns = gameNight.Sessions[0];
        Assert.Equal(GameNightSessionStatus.InProgress, gns.Status);
    }
}
