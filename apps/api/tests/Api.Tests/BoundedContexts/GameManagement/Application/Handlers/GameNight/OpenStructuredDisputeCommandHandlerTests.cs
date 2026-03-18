using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

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
        Assert.NotEqual(Guid.Empty, disputeId);

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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _openHandler.Handle(command, CancellationToken.None));

        Assert.Contains("disabled", ex.Message);
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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _openHandler.Handle(command, CancellationToken.None));
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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _openHandler.Handle(command, CancellationToken.None));

        Assert.Contains("no associated game", ex.Message);
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
        Assert.NotEqual(Guid.Empty, disputeId);

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
        Assert.Equal(DefaultRespondentPlayerId, dispute.RespondentPlayerId);
        Assert.Equal("I disagree, the rule says otherwise", dispute.RespondentClaim);

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
        await Assert.ThrowsAsync<NotFoundException>(
            () => _respondHandler.Handle(command, CancellationToken.None));
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
        Assert.Null(dispute.RespondentPlayerId);
        Assert.Null(dispute.RespondentClaim);
    }

    [Fact]
    public async Task RespondentTimeout_DisputeNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        SetupDisputeGetById(disputeId, null);

        var command = new RespondentTimeoutCommand(disputeId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _timeoutHandler.Handle(command, CancellationToken.None));
    }
}
