using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Session;

/// <summary>
/// Unit tests for CreateSessionInviteCommandHandler.
/// E3-1: Session Invite Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateSessionInviteCommandHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly CreateSessionInviteCommandHandler _sut;

    private static readonly Guid HostUserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    public CreateSessionInviteCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sut = new CreateSessionInviteCommandHandler(_dbContext);
    }

    private async Task SeedSession(Guid sessionId, Guid createdByUserId)
    {
        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = sessionId,
            SessionCode = "TEST01",
            GameName = "Test Game",
            CreatedByUserId = createdByUserId,
            Status = 2, // InProgress
            CurrentTurnIndex = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Visibility = 0,
            AgentMode = 0,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenHostCreatesInvite_ShouldReturnPin()
    {
        await SeedSession(SessionId, HostUserId);

        var command = new CreateSessionInviteCommand(SessionId, HostUserId, MaxUses: 10, ExpiryMinutes: 30);
        var result = await _sut.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Pin);
        Assert.Equal(6, result.Pin.Length);
        Assert.NotNull(result.LinkToken);
        Assert.Equal(32, result.LinkToken.Length);
        Assert.Equal(10, result.MaxUses);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WhenNotHost_ShouldThrowForbidden()
    {
        await SeedSession(SessionId, HostUserId);

        var otherUserId = Guid.NewGuid();
        var command = new CreateSessionInviteCommand(SessionId, otherUserId, MaxUses: 10, ExpiryMinutes: 30);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ShouldThrowNotFound()
    {
        var command = new CreateSessionInviteCommand(Guid.NewGuid(), HostUserId, MaxUses: 10, ExpiryMinutes: 30);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPersistInviteToDatabase()
    {
        await SeedSession(SessionId, HostUserId);

        var command = new CreateSessionInviteCommand(SessionId, HostUserId, MaxUses: 5, ExpiryMinutes: 15);
        var result = await _sut.Handle(command, CancellationToken.None);

        var saved = await _dbContext.SessionInvites.FindAsync(
            _dbContext.SessionInvites.First().Id);
        Assert.NotNull(saved);
        Assert.Equal(result.Pin, saved.Pin);
        Assert.Equal(result.LinkToken, saved.LinkToken);
        Assert.Equal(5, saved.MaxUses);
        Assert.Equal(0, saved.CurrentUses);
        Assert.False(saved.IsRevoked);
    }
}
