using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Session;

/// <summary>
/// Unit tests for JoinSessionCommandHandler.
/// E3-1: Session Invite Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class JoinSessionCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ITierEnforcementService> _tierServiceMock;
    private readonly JoinSessionCommandHandler _sut;

    private static readonly Guid HostUserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private const string TestPin = "ABC123";
    private const string TestLinkToken = "abcdef01234567890abcdef012345678";

    public JoinSessionCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _tierServiceMock = new Mock<ITierEnforcementService>();

        // Default tier limits: allow 12 players
        _tierServiceMock
            .Setup(s => s.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.PremiumTier);

        _sut = new JoinSessionCommandHandler(_dbContext, _tierServiceMock.Object);
    }

    private async Task SeedSessionAndInvite(
        Guid sessionId,
        Guid hostUserId,
        string pin = TestPin,
        string linkToken = TestLinkToken,
        int maxUses = 10,
        DateTime? expiresAt = null,
        bool isRevoked = false,
        int currentUses = 0)
    {
        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = sessionId,
            SessionCode = "TEST01",
            GameName = "Test Game",
            CreatedByUserId = hostUserId,
            Status = 2,
            CurrentTurnIndex = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Visibility = 0,
            AgentMode = 0,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });

        _dbContext.SessionInvites.Add(new SessionInviteEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            CreatedByUserId = hostUserId,
            Pin = pin,
            LinkToken = linkToken,
            MaxUses = maxUses,
            CurrentUses = currentUses,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            IsRevoked = isRevoked
        });

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithValidPin_ShouldCreateParticipant()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var command = new JoinSessionCommand(TestPin, GuestName: "Alice", UserId: null);
        var result = await _sut.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Equal("Alice", result.DisplayName);
        Assert.Equal("Player", result.Role);
        Assert.NotNull(result.ConnectionToken);
        Assert.Equal(6, result.ConnectionToken.Length);
    }

    [Fact]
    public async Task Handle_WithValidLinkToken_ShouldCreateParticipant()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var userId = Guid.NewGuid();
        var command = new JoinSessionCommand(TestLinkToken, GuestName: null, UserId: userId);
        var result = await _sut.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Equal("Player", result.Role);
    }

    [Fact]
    public async Task Handle_WithExpiredInvite_ShouldThrowConflict()
    {
        await SeedSessionAndInvite(SessionId, HostUserId, expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var command = new JoinSessionCommand(TestPin, GuestName: "Alice", UserId: null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithRevokedInvite_ShouldThrowConflict()
    {
        await SeedSessionAndInvite(SessionId, HostUserId, isRevoked: true);

        var command = new JoinSessionCommand(TestPin, GuestName: "Alice", UserId: null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithMaxUsesReached_ShouldThrowConflict()
    {
        await SeedSessionAndInvite(SessionId, HostUserId, maxUses: 2, currentUses: 2);

        var command = new JoinSessionCommand(TestPin, GuestName: "Alice", UserId: null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GuestWithoutName_ShouldThrowValidation()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var command = new JoinSessionCommand(TestPin, GuestName: null, UserId: null);

        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_GuestWithEmptyName_ShouldThrowValidation()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var command = new JoinSessionCommand(TestPin, GuestName: "   ", UserId: null);

        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAlreadyJoined_ShouldThrowConflict()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var userId = Guid.NewGuid();

        // Seed an existing active participant for this user
        _dbContext.SessionParticipants.Add(new SessionParticipantEntity
        {
            Id = Guid.NewGuid(),
            SessionId = SessionId,
            UserId = userId,
            Role = "Player",
            AgentAccessEnabled = false,
            ConnectionToken = "XYZ789",
            JoinedAt = DateTime.UtcNow,
            LeftAt = null
        });
        await _dbContext.SaveChangesAsync();

        var command = new JoinSessionCommand(TestPin, GuestName: null, UserId: userId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ShouldThrowNotFound()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var command = new JoinSessionCommand("INVALID", GuestName: "Alice", UserId: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldIncrementInviteCurrentUses()
    {
        await SeedSessionAndInvite(SessionId, HostUserId);

        var command = new JoinSessionCommand(TestPin, GuestName: "Alice", UserId: null);
        await _sut.Handle(command, CancellationToken.None);

        var invite = _dbContext.SessionInvites.First();
        Assert.Equal(1, invite.CurrentUses);
    }

    [Fact]
    public async Task Handle_WhenSessionFull_ShouldThrowConflict()
    {
        // Set tier to allow only 1 player
        _tierServiceMock
            .Setup(s => s.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.Create(3, 3, 50L * 1024 * 1024, 1, 20, 30, 1, 5, false, 1));

        await SeedSessionAndInvite(SessionId, HostUserId);

        // Seed an existing active participant to fill the session
        _dbContext.SessionParticipants.Add(new SessionParticipantEntity
        {
            Id = Guid.NewGuid(),
            SessionId = SessionId,
            UserId = Guid.NewGuid(),
            Role = "Player",
            AgentAccessEnabled = false,
            ConnectionToken = "AAA111",
            JoinedAt = DateTime.UtcNow,
            LeftAt = null
        });
        await _dbContext.SaveChangesAsync();

        var command = new JoinSessionCommand(TestPin, GuestName: "Alice", UserId: null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }
}
