using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionStateTierTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IAutoSaveSchedulerService> _mockAutoSaveScheduler;
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionStateTierTests()
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

    private static GameNightEvent CreatePublishedEvent(Guid organizerId)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Friday Night", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        return evt;
    }

    private static UserDto CreateOrganizerDto(Guid organizerId) =>
        new UserDto(
            Id: organizerId,
            Email: "org@test.com",
            DisplayName: "Organizer",
            Role: "User",
            Tier: "Free",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null,
            Level: 1,
            ExperiencePoints: 0);

    private static CreateSessionResult MakeSessionResult(Guid sessionId, Guid gameNightId) =>
        new CreateSessionResult(
            sessionId,
            "ABC123",
            [],
            GameNightEventId: gameNightId,
            GameNightWasCreated: false,
            AgentDefinitionId: null,
            ToolkitId: null);

    [Fact]
    public async Task Handle_WithStateTierFull_PropagatesStateTierToCreateSessionCommand()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(organizerId));

        CreateSessionCommand? captured = null;
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateSessionResult>, CancellationToken>(
                (cmd, _) => captured = cmd as CreateSessionCommand)
            .ReturnsAsync(MakeSessionResult(Guid.NewGuid(), gameNight.Id));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, Guid.NewGuid(), "Terraforming Mars", organizerId,
            StateTier: GameStateTier.Full);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(GameStateTier.Full, captured.StateTier);
    }

    [Fact]
    public async Task Handle_WithoutStateTier_DefaultsToMinimal()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var gameNight = CreatePublishedEvent(organizerId);

        _mockRepository.Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(organizerId));

        CreateSessionCommand? captured = null;
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateSessionResult>, CancellationToken>(
                (cmd, _) => captured = cmd as CreateSessionCommand)
            .ReturnsAsync(MakeSessionResult(Guid.NewGuid(), gameNight.Id));

        // No StateTier — should default to Minimal
        var command = new StartGameNightSessionCommand(
            gameNight.Id, Guid.NewGuid(), "Dixit", organizerId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(GameStateTier.Minimal, captured.StateTier);
    }
}
