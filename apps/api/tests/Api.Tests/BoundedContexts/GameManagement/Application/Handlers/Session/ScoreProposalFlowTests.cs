using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Hubs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Session;

/// <summary>
/// Unit tests for ProposeScoreCommandHandler and ConfirmScoreProposalCommandHandler.
/// E3-3: Score Proposal Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class ScoreProposalFlowTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IHubContext<GameStateHub>> _hubContextMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly ProposeScoreCommandHandler _proposeSut;
    private readonly ConfirmScoreProposalCommandHandler _confirmSut;

    private static readonly Guid HostUserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();
    private static readonly Guid TargetPlayerId = Guid.NewGuid();

    public ScoreProposalFlowTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _hubContextMock = new Mock<IHubContext<GameStateHub>>();
        _clientProxyMock = new Mock<IClientProxy>();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();

        // Setup hub context mock chain
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(_clientProxyMock.Object);
        _hubContextMock
            .Setup(h => h.Clients)
            .Returns(hubClientsMock.Object);

        var proposeLogger = new Mock<ILogger<ProposeScoreCommandHandler>>();
        _proposeSut = new ProposeScoreCommandHandler(
            _dbContext,
            _hubContextMock.Object,
            proposeLogger.Object);

        var confirmLogger = new Mock<ILogger<ConfirmScoreProposalCommandHandler>>();
        _confirmSut = new ConfirmScoreProposalCommandHandler(
            _dbContext,
            _sessionRepoMock.Object,
            _hubContextMock.Object,
            confirmLogger.Object);
    }

    private async Task SeedSessionWithParticipant(
        Guid sessionId,
        Guid hostUserId,
        Guid participantId,
        int status = 2) // InProgress
    {
        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = sessionId,
            SessionCode = "TEST01",
            GameName = "Test Game",
            CreatedByUserId = hostUserId,
            Status = status,
            CurrentTurnIndex = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Visibility = 0,
            AgentMode = 0,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });

        _dbContext.SessionParticipants.Add(new SessionParticipantEntity
        {
            Id = participantId,
            SessionId = sessionId,
            UserId = Guid.NewGuid(),
            Role = "Player",
            AgentAccessEnabled = false,
            ConnectionToken = "ABC123",
            JoinedAt = DateTime.UtcNow,
            LeftAt = null
        });

        await _dbContext.SaveChangesAsync();
    }

    // ── ProposeScore Tests ──

    [Fact]
    public async Task ProposeScore_ValidParticipant_ShouldNotifyHost()
    {
        // Arrange
        await SeedSessionWithParticipant(SessionId, HostUserId, ParticipantId);

        var command = new ProposeScoreCommand(
            SessionId, ParticipantId, TargetPlayerId,
            Round: 1, Dimension: "Points", Value: 42, ProposerName: "Alice");

        // Act
        await _proposeSut.Handle(command, CancellationToken.None);

        // Assert — SignalR sent to host group
        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ScoreProposed",
                It.Is<object?[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProposeScore_SessionNotFound_ShouldThrow()
    {
        // Arrange
        var command = new ProposeScoreCommand(
            Guid.NewGuid(), ParticipantId, TargetPlayerId,
            Round: 1, Dimension: "Points", Value: 10, ProposerName: null);

        // Act & Assert
        var act = () =>
            _proposeSut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ProposeScore_ParticipantNotInSession_ShouldThrow()
    {
        // Arrange — seed session but with a different participant
        await SeedSessionWithParticipant(SessionId, HostUserId, Guid.NewGuid());

        var command = new ProposeScoreCommand(
            SessionId, ParticipantId, TargetPlayerId,
            Round: 1, Dimension: "Points", Value: 10, ProposerName: null);

        // Act & Assert
        var act = () =>
            _proposeSut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ProposeScore_SessionCompleted_ShouldThrowConflict()
    {
        // Arrange — status 4 = Completed
        await SeedSessionWithParticipant(SessionId, HostUserId, ParticipantId, status: 4);

        var command = new ProposeScoreCommand(
            SessionId, ParticipantId, TargetPlayerId,
            Round: 1, Dimension: "Points", Value: 10, ProposerName: null);

        // Act & Assert
        var act = () =>
            _proposeSut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    // ── ConfirmScore Tests ──

    [Fact]
    public async Task ConfirmScore_HostConfirms_ShouldRecordAndBroadcast()
    {
        // Arrange
        await SeedSessionWithParticipant(SessionId, HostUserId, ParticipantId);

        // Create a real domain LiveGameSession (internal sealed — can't mock)
        var scoringConfig = new SessionScoringConfig(new[] { "Points" });
        var domainSession = LiveGameSession.Create(
            SessionId, HostUserId, "Test Game", scoringConfig: scoringConfig);
        var player = domainSession.AddPlayer(null, "Alice", PlayerColor.Red);
        domainSession.Start();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainSession);

        var command = new ConfirmScoreProposalCommand(
            SessionId, HostUserId, player.Id,
            Round: 1, Dimension: "Points", Value: 42);

        // Act
        await _confirmSut.Handle(command, CancellationToken.None);

        // Assert — persisted
        _sessionRepoMock.Verify(
            r => r.UpdateAsync(domainSession, It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — broadcast to all
        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ScoreConfirmed",
                It.Is<object?[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmScore_NonHost_ShouldThrowForbidden()
    {
        // Arrange
        await SeedSessionWithParticipant(SessionId, HostUserId, ParticipantId);

        var nonHostUserId = Guid.NewGuid();
        var command = new ConfirmScoreProposalCommand(
            SessionId, nonHostUserId, TargetPlayerId,
            Round: 1, Dimension: "Points", Value: 10);

        // Act & Assert
        var act = () =>
            _confirmSut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ConfirmScore_SessionNotFound_ShouldThrow()
    {
        // Arrange
        var command = new ConfirmScoreProposalCommand(
            Guid.NewGuid(), HostUserId, TargetPlayerId,
            Round: 1, Dimension: "Points", Value: 10);

        // Act & Assert
        var act = () =>
            _confirmSut.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
