using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Unit tests for TallyDisputeVotesCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class TallyDisputeVotesCommandHandlerTests
{
    private readonly Mock<IRuleDisputeRepository> _disputeRepositoryMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly TallyDisputeVotesCommandHandler _handler;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultGameId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultInitiatorPlayerId = Guid.NewGuid();

    public TallyDisputeVotesCommandHandlerTests()
    {
        _disputeRepositoryMock = new Mock<IRuleDisputeRepository>();
        _sessionRepositoryMock = new Mock<ILiveSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new TallyDisputeVotesCommandHandler(
            _disputeRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    // === Helpers ===

    private static RuleDispute CreateDisputeWithVerdictAndVotes(
        int acceptCount,
        int rejectCount)
    {
        var dispute = RuleDispute.Open(
            DefaultSessionId, DefaultGameId, DefaultInitiatorPlayerId, "Test claim");

        dispute.SetVerdict(new DisputeVerdict(
            RulingFor.Initiator,
            "The rule clearly states one move per turn.",
            "Rulebook p.12",
            VerdictConfidence.High));

        for (var i = 0; i < acceptCount; i++)
            dispute.CastVote(Guid.NewGuid(), acceptsVerdict: true);

        for (var i = 0; i < rejectCount; i++)
            dispute.CastVote(Guid.NewGuid(), acceptsVerdict: false);

        return dispute;
    }

    private static LiveGameSession CreateSession(Guid? sessionId = null)
    {
        return LiveGameSession.Create(
            sessionId ?? DefaultSessionId,
            DefaultUserId,
            "Test Game",
            TimeProvider.System,
            gameId: DefaultGameId);
    }

    private void SetupDisputeGetById(Guid disputeId, RuleDispute? dispute)
    {
        _disputeRepositoryMock
            .Setup(x => x.GetByIdAsync(disputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
    }

    private void SetupSessionGetById(Guid sessionId, LiveGameSession? session)
    {
        _sessionRepositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    // =============================================
    // TallyDisputeVotesCommand tests
    // =============================================

    [Fact]
    public async Task TallyVotes_MajorityAccepts_FinalOutcomeIsVerdictAccepted()
    {
        // Arrange
        var dispute = CreateDisputeWithVerdictAndVotes(acceptCount: 3, rejectCount: 1);
        var disputeId = dispute.Id;
        var session = CreateSession();

        SetupDisputeGetById(disputeId, dispute);
        SetupSessionGetById(DefaultSessionId, session);

        var command = new TallyDisputeVotesCommand(disputeId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(DisputeOutcome.VerdictAccepted, dispute.FinalOutcome);
        Assert.Null(dispute.OverrideRule);

        _disputeRepositoryMock.Verify(
            x => x.UpdateAsync(dispute, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TallyVotes_MajorityRejectsWithOverrideRule_FinalOutcomeIsVerdictOverridden()
    {
        // Arrange
        var dispute = CreateDisputeWithVerdictAndVotes(acceptCount: 1, rejectCount: 3);
        var disputeId = dispute.Id;
        var session = CreateSession();

        SetupDisputeGetById(disputeId, dispute);
        SetupSessionGetById(DefaultSessionId, session);

        var command = new TallyDisputeVotesCommand(disputeId, OverrideRule: "House rule: two moves allowed");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(DisputeOutcome.VerdictOverridden, dispute.FinalOutcome);
        Assert.Equal("House rule: two moves allowed", dispute.OverrideRule);

        _disputeRepositoryMock.Verify(
            x => x.UpdateAsync(dispute, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TallyVotes_AppendsLegacyEntryToSession()
    {
        // Arrange
        var dispute = CreateDisputeWithVerdictAndVotes(acceptCount: 2, rejectCount: 1);
        var disputeId = dispute.Id;
        var session = CreateSession();

        SetupDisputeGetById(disputeId, dispute);
        SetupSessionGetById(DefaultSessionId, session);

        var command = new TallyDisputeVotesCommand(disputeId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — session should have the legacy dispute entry appended
        Assert.Single(session.Disputes);
        Assert.Equal(disputeId, session.Disputes[0].Id);

        _sessionRepositoryMock.Verify(
            x => x.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TallyVotes_DisputeNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        SetupDisputeGetById(disputeId, null);

        var command = new TallyDisputeVotesCommand(disputeId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
