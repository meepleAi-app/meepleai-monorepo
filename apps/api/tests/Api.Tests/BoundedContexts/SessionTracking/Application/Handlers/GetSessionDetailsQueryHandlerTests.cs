using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// #4883: the session details carry the session code, who sits at the table, the scores and the shared
/// notes, and the endpoint answered them to any authenticated user who had the session id. The code is the
/// credential that lets a stranger join, so this read is owner/participant-only; the cross-context readers
/// (the game-night bridge) use a separate system query that has no caller to authorize.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GetSessionDetailsQueryHandlerTests : IDisposable
{
    private const string SessionCode = "DET4SZ";

    private readonly MeepleAiDbContext _context;
    private readonly GetSessionDetailsQueryHandler _handler;
    private readonly GetSessionDetailsForSystemQueryHandler _systemHandler;

    public GetSessionDetailsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new GetSessionDetailsQueryHandler(_context);
        _systemHandler = new GetSessionDetailsForSystemQueryHandler(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_Owner_ReturnsDetailsWithTheSessionCode()
    {
        var ownerId = Guid.NewGuid();
        var sessionId = await SeedAsync(ownerId);

        var result = await _handler.Handle(new GetSessionDetailsQuery(sessionId, ownerId), CancellationToken.None);

        Assert.Equal(sessionId, result.Id);
        Assert.Equal(SessionCode, result.SessionCode);
    }

    [Fact]
    public async Task Handle_RegisteredParticipantWhoIsNotOwner_ReturnsDetails()
    {
        // The other direction: the people at the table are the ones who must keep reading it.
        var ownerId = Guid.NewGuid();
        var playerUserId = Guid.NewGuid();
        var sessionId = await SeedAsync(ownerId, playerUserId);

        var result = await _handler.Handle(new GetSessionDetailsQuery(sessionId, playerUserId), CancellationToken.None);

        Assert.Equal(SessionCode, result.SessionCode);
        Assert.Equal(2, result.Participants.Count);
    }

    [Fact]
    public async Task Handle_StrangerWhoHoldsOnlyTheSessionId_ThrowsForbidden()
    {
        // The attack: no code, no seat, just the id (it is in the share link) — and the details used to
        // answer with the code that opens the session.
        var sessionId = await SeedAsync(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(new GetSessionDetailsQuery(sessionId, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmptyRequester_ThrowsForbidden()
    {
        var sessionId = await SeedAsync(Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(new GetSessionDetailsQuery(sessionId, Guid.Empty), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownSession_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(new GetSessionDetailsQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task SystemHandle_WithNoCaller_ReturnsTheSameDetails()
    {
        // The game-night bridge reads the companion session for the organizer, who need not sit in it
        // (a draft planned by someone else): it must keep working without a caller to authorize.
        var ownerId = Guid.NewGuid();
        var sessionId = await SeedAsync(ownerId, Guid.NewGuid());

        var forOwner = await _handler.Handle(new GetSessionDetailsQuery(sessionId, ownerId), CancellationToken.None);
        var forSystem = await _systemHandler.Handle(new GetSessionDetailsForSystemQuery(sessionId), CancellationToken.None);

        Assert.Equal(forOwner.Id, forSystem.Id);
        Assert.Equal(forOwner.SessionCode, forSystem.SessionCode);
        Assert.Equal(
            forOwner.Participants.Select(p => p.Id).Order(),
            forSystem.Participants.Select(p => p.Id).Order());
    }

    [Fact]
    public async Task SystemHandle_UnknownSession_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _systemHandler.Handle(new GetSessionDetailsForSystemQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private async Task<Guid> SeedAsync(Guid ownerId, Guid? playerUserId = null)
    {
        var sessionId = Guid.NewGuid();

        var participants = new List<ParticipantEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = ownerId,
                DisplayName = "Owner",
                IsOwner = true,
                Role = ParticipantRole.Host,
                JoinOrder = 1,
                CreatedAt = DateTime.UtcNow
            }
        };

        if (playerUserId is { } player)
        {
            participants.Add(new ParticipantEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = player,
                DisplayName = "Player",
                IsOwner = false,
                Role = ParticipantRole.Player,
                JoinOrder = 2,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = sessionId,
            UserId = ownerId,
            GameId = Guid.NewGuid(),
            SessionCode = SessionCode,
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = ownerId,
            Participants = participants
        });

        await _context.SaveChangesAsync();
        return sessionId;
    }
}
