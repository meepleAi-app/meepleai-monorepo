using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.Hubs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Session;

/// <summary>
/// Tests for ToggleAgentAccessCommandHandler.
/// E3-2: Host toggles AI agent access for a specific session participant.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ToggleAgentAccessCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IHubContext<GameStateHub>> _hubContextMock;
    private readonly Mock<ILogger<ToggleAgentAccessCommandHandler>> _loggerMock;
    private readonly ToggleAgentAccessCommandHandler _handler;

    public ToggleAgentAccessCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"ToggleAgentAccess_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _hubContextMock = new Mock<IHubContext<GameStateHub>>();
        _loggerMock = new Mock<ILogger<ToggleAgentAccessCommandHandler>>();

        // Setup SignalR mock chain — handler broadcasts to Group, not User
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        _hubContextMock.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new ToggleAgentAccessCommandHandler(
            _dbContext,
            _hubContextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_HostTogglesOn_ShouldUpdateAndNotify()
    {
        // Arrange
        var hostUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        await SeedSessionAndParticipant(sessionId, hostUserId, participantId, agentAccessEnabled: false);

        var command = new ToggleAgentAccessCommand(sessionId, participantId, hostUserId, Enabled: true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var participant = await _dbContext.SessionParticipants.FindAsync(participantId);
        participant.Should().NotBeNull();
        (participant.AgentAccessEnabled).Should().BeTrue();

        _hubContextMock.Verify(h =>
            h.Clients.Group($"session:{sessionId}"), Times.Once);
    }

    [Fact]
    public async Task Handle_HostTogglesOff_ShouldUpdateAndNotify()
    {
        // Arrange
        var hostUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        await SeedSessionAndParticipant(sessionId, hostUserId, participantId, agentAccessEnabled: true);

        var command = new ToggleAgentAccessCommand(sessionId, participantId, hostUserId, Enabled: false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var participant = await _dbContext.SessionParticipants.FindAsync(participantId);
        participant.Should().NotBeNull();
        (participant.AgentAccessEnabled).Should().BeFalse();

        _hubContextMock.Verify(h =>
            h.Clients.Group($"session:{sessionId}"), Times.Once);
    }

    [Fact]
    public async Task Handle_NonHost_ShouldThrowForbidden()
    {
        // Arrange
        var hostUserId = Guid.NewGuid();
        var nonHostUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        await SeedSessionAndParticipant(sessionId, hostUserId, participantId, agentAccessEnabled: false);

        var command = new ToggleAgentAccessCommand(sessionId, participantId, nonHostUserId, Enabled: true);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_SessionNotFound_ShouldThrow()
    {
        // Arrange
        var command = new ToggleAgentAccessCommand(
            Guid.NewGuid(), // non-existent session
            Guid.NewGuid(),
            Guid.NewGuid(),
            Enabled: true);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ParticipantNotFound_ShouldThrow()
    {
        // Arrange
        var hostUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Seed session but no participant
        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = sessionId,
            CreatedByUserId = hostUserId,
            SessionCode = "TEST01",
            GameName = "Test Game",
            Status = 2, // InProgress
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });
        await _dbContext.SaveChangesAsync();

        var command = new ToggleAgentAccessCommand(
            sessionId,
            Guid.NewGuid(), // non-existent participant
            hostUserId,
            Enabled: true);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private async Task SeedSessionAndParticipant(
        Guid sessionId, Guid hostUserId, Guid participantId, bool agentAccessEnabled)
    {
        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = sessionId,
            CreatedByUserId = hostUserId,
            SessionCode = "TEST01",
            GameName = "Test Game",
            Status = 2, // InProgress
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });

        _dbContext.SessionParticipants.Add(new SessionParticipantEntity
        {
            Id = participantId,
            SessionId = sessionId,
            UserId = Guid.NewGuid(),
            Role = "Player",
            AgentAccessEnabled = agentAccessEnabled,
            ConnectionToken = "ABC123",
            JoinedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
