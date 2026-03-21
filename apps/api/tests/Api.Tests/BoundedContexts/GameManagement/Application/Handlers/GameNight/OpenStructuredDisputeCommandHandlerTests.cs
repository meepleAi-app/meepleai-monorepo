using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Unit tests for OpenStructuredDisputeCommandHandler, RespondToDisputeCommandHandler,
/// and RespondentTimeoutCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class OpenStructuredDisputeCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IRuleDisputeRepository> _disputeRepositoryMock;
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly OpenStructuredDisputeCommandHandler _openHandler;
    private readonly RespondToDisputeCommandHandler _respondHandler;
    private readonly RespondentTimeoutCommandHandler _timeoutHandler;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultGameId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultInitiatorPlayerId = Guid.NewGuid();
    private static readonly Guid DefaultRespondentPlayerId = Guid.NewGuid();

    public OpenStructuredDisputeCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ILiveSessionRepository>();
        _disputeRepositoryMock = new Mock<IRuleDisputeRepository>();
        _featureFlagServiceMock = new Mock<IFeatureFlagService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _openHandler = new OpenStructuredDisputeCommandHandler(
            _sessionRepositoryMock.Object,
            _disputeRepositoryMock.Object,
            _featureFlagServiceMock.Object,
            _unitOfWorkMock.Object);

        _respondHandler = new RespondToDisputeCommandHandler(
            _disputeRepositoryMock.Object,
            _unitOfWorkMock.Object);

        _timeoutHandler = new RespondentTimeoutCommandHandler(
            _disputeRepositoryMock.Object);
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
            .Setup(x => x.IsEnabledAsync("Features:Arbitro.StructuredDisputes", null))
            .ReturnsAsync(enabled);
    }

    private void SetupSessionGetById(Guid sessionId, LiveGameSession? session)
    {
        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupDisputeGetById(Guid disputeId, RuleDispute? dispute)
    {
        _disputeRepositoryMock
            .Setup(x => x.GetByIdAsync(disputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
    }

    private void SetupGameDisputeHistory(Guid gameId, IReadOnlyList<RuleDispute> disputes)
    {
        _disputeRepositoryMock
            .Setup(x => x.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(disputes);
    }

    // =============================================
    // OpenStructuredDisputeCommand tests
    // =============================================

    [Fact]
    public async Task OpenStructuredDispute_ValidCommand_CreatesDisputeAndReturnsId()
    {
        // Arrange
        var session = CreateSessionWithGameId();
        SetupFeatureFlagEnabled();
        SetupSessionGetById(DefaultSessionId, session);
        SetupGameDisputeHistory(DefaultGameId, Array.Empty<RuleDispute>());

        var command = new OpenStructuredDisputeCommand(
            DefaultSessionId,
            DefaultInitiatorPlayerId,
            "The player moved two spaces but should only move one");

        // Act
        var disputeId = await _openHandler.Handle(command, CancellationToken.None);

        // Assert
        disputeId.Should().NotBe(Guid.Empty);

        _disputeRepositoryMock.Verify(
            x => x.AddAsync(It.Is<RuleDispute>(d =>
                d.SessionId == DefaultSessionId &&
                d.GameId == DefaultGameId &&
                d.InitiatorPlayerId == DefaultInitiatorPlayerId &&
                d.InitiatorClaim == "The player moved two spaces but should only move one"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenStructuredDispute_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupFeatureFlagEnabled(false);

        var command = new OpenStructuredDisputeCommand(
            DefaultSessionId,
            DefaultInitiatorPlayerId,
            "Some claim");

        // Act & Assert
        var act =
            () => _openHandler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task OpenStructuredDispute_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupFeatureFlagEnabled();
        SetupSessionGetById(DefaultSessionId, null);

        var command = new OpenStructuredDisputeCommand(
            DefaultSessionId,
            DefaultInitiatorPlayerId,
            "Some claim");

        // Act & Assert
        var act =
            () => _openHandler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task OpenStructuredDispute_SessionWithoutGameId_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionWithoutGameId();
        SetupFeatureFlagEnabled();
        SetupSessionGetById(DefaultSessionId, session);

        var command = new OpenStructuredDisputeCommand(
            DefaultSessionId,
            DefaultInitiatorPlayerId,
            "Some claim");

        // Act & Assert
        var act =
            () => _openHandler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Contain("no associated game");
    }

    [Fact]
    public async Task OpenStructuredDispute_WithExistingGameHistory_SetsRelatedDisputeIds()
    {
        // Arrange
        var session = CreateSessionWithGameId();
        SetupFeatureFlagEnabled();
        SetupSessionGetById(DefaultSessionId, session);

        // Create 4 historical disputes — handler should take the last 3
        var historicalDisputes = new List<RuleDispute>
        {
            RuleDispute.Open(Guid.NewGuid(), DefaultGameId, Guid.NewGuid(), "Old dispute 1"),
            RuleDispute.Open(Guid.NewGuid(), DefaultGameId, Guid.NewGuid(), "Old dispute 2"),
            RuleDispute.Open(Guid.NewGuid(), DefaultGameId, Guid.NewGuid(), "Old dispute 3"),
            RuleDispute.Open(Guid.NewGuid(), DefaultGameId, Guid.NewGuid(), "Old dispute 4"),
        };

        SetupGameDisputeHistory(DefaultGameId, historicalDisputes);

        var command = new OpenStructuredDisputeCommand(
            DefaultSessionId,
            DefaultInitiatorPlayerId,
            "New dispute claim");

        // Act
        var disputeId = await _openHandler.Handle(command, CancellationToken.None);

        // Assert
        disputeId.Should().NotBe(Guid.Empty);

        _disputeRepositoryMock.Verify(
            x => x.AddAsync(It.Is<RuleDispute>(d =>
                d.RelatedDisputeIds.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // =============================================
    // RespondToDisputeCommand tests
    // =============================================

    [Fact]
    public async Task RespondToDispute_ValidCommand_SetsRespondentClaim()
    {
        // Arrange
        var dispute = RuleDispute.Open(
            DefaultSessionId, DefaultGameId, DefaultInitiatorPlayerId, "Initiator claim");
        var disputeId = dispute.Id;

        SetupDisputeGetById(disputeId, dispute);

        var command = new RespondToDisputeCommand(
            disputeId,
            DefaultRespondentPlayerId,
            "I disagree, the rule says otherwise");

        // Act
        await _respondHandler.Handle(command, CancellationToken.None);

        // Assert
        dispute.RespondentPlayerId.Should().Be(DefaultRespondentPlayerId);
        dispute.RespondentClaim.Should().Be("I disagree, the rule says otherwise");

        _disputeRepositoryMock.Verify(
            x => x.UpdateAsync(dispute, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondToDispute_DisputeNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        SetupDisputeGetById(disputeId, null);

        var command = new RespondToDisputeCommand(
            disputeId,
            DefaultRespondentPlayerId,
            "Some response");

        // Act & Assert
        var act =
            () => _respondHandler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // =============================================
    // RespondentTimeoutCommand tests
    // =============================================

    [Fact]
    public async Task RespondentTimeout_ValidWithNoRespondent_Succeeds()
    {
        // Arrange
        var dispute = RuleDispute.Open(
            DefaultSessionId, DefaultGameId, DefaultInitiatorPlayerId, "Initiator claim");
        var disputeId = dispute.Id;

        SetupDisputeGetById(disputeId, dispute);

        var command = new RespondentTimeoutCommand(disputeId);

        // Act — should not throw (no-op)
        await _timeoutHandler.Handle(command, CancellationToken.None);

        // Assert — respondent should remain null
        dispute.RespondentPlayerId.Should().BeNull();
        dispute.RespondentClaim.Should().BeNull();
    }

    [Fact]
    public async Task RespondentTimeout_DisputeNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        SetupDisputeGetById(disputeId, null);

        var command = new RespondentTimeoutCommand(disputeId);

        // Act & Assert
        var act =
            () => _timeoutHandler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
