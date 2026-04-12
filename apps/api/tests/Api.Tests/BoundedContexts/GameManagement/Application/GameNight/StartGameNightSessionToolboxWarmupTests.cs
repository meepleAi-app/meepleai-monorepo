using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for the auto toolbox warm-up behavior in StartGameNightSessionCommandHandler (GAP-003).
/// Validates that a ToolboxTemplate is applied fire-and-forget after session creation,
/// and that failures never propagate to the caller.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionToolboxWarmupTests
{
    private readonly Mock<IGameNightEventRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IAutoSaveSchedulerService> _mockAutoSaveScheduler;
    private readonly StartGameNightSessionCommandHandler _handler;

    public StartGameNightSessionToolboxWarmupTests()
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

    private static UserDto CreateOrganizerDto(Guid organizerId)
    {
        return new UserDto(
            Id: organizerId,
            Email: "org@test.com",
            DisplayName: "Organizer",
            Role: "User",
            Tier: "Free",
            CreatedAt: DateTime.UtcNow,
            IsTwoFactorEnabled: false,
            TwoFactorEnabledAt: null,
            Level: 1,
            ExperiencePoints: 0,
            EmailVerified: true,
            EmailVerifiedAt: DateTime.UtcNow,
            VerificationGracePeriodEndsAt: null,
            OnboardingCompleted: true,
            OnboardingSkipped: false);
    }

    private static ToolboxTemplateDto CreateTemplateDto(Guid templateId, Guid? gameId = null)
    {
        return new ToolboxTemplateDto(
            Id: templateId,
            Name: "Default Template",
            GameId: gameId,
            Mode: "Freeform",
            Source: "User",
            ToolsJson: "[]",
            PhasesJson: "[]",
            CreatedAt: DateTime.UtcNow);
    }

    private void SetupSuccessfulSessionCreation(GameNightEvent gameNight)
    {
        _mockRepository
            .Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrganizerDto(gameNight.OrganizerId));

        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(),
                "ABC123",
                [],
                GameNightEventId: gameNight.Id,
                GameNightWasCreated: false,
                AgentDefinitionId: null,
                ToolkitId: null));
    }

    [Fact]
    public async Task Handle_WhenToolboxTemplateExists_AppliesTemplateFireAndForget()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];
        var templateId = Guid.NewGuid();

        SetupSuccessfulSessionCreation(gameNight);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetToolboxTemplatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTemplateDto(templateId, gameId)]);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ApplyToolboxTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolboxDto)null!);

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — template was applied exactly once with the correct template id and game id
        Assert.NotNull(result);
        _mockMediator.Verify(m => m.Send(
            It.Is<ApplyToolboxTemplateCommand>(c => c.TemplateId == templateId && c.GameId == gameId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoToolboxTemplateExists_DoesNotDispatchApplyTemplate()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];

        SetupSuccessfulSessionCreation(gameNight);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetToolboxTemplatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — no template found, ApplyToolboxTemplateCommand should never be dispatched
        Assert.NotNull(result);
        _mockMediator.Verify(m => m.Send(
            It.IsAny<ApplyToolboxTemplateCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenToolboxWarmupFails_StillReturnsSuccessResult()
    {
        // Arrange
        var gameNight = CreatePublishedEvent();
        var gameId = gameNight.GameIds[0];
        var templateId = Guid.NewGuid();

        SetupSuccessfulSessionCreation(gameNight);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetToolboxTemplatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTemplateDto(templateId, gameId)]);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ApplyToolboxTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Toolbox apply failed"));

        var command = new StartGameNightSessionCommand(
            gameNight.Id, gameId, "Catan", gameNight.OrganizerId);

        // Act — must NOT throw even though ApplyToolboxTemplateCommand throws
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — handler swallowed the exception and returned a valid result
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SessionId);
    }
}
