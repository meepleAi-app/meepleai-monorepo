using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Shortcut command that creates a LiveGameSession + SessionInvite in one transaction
/// for the Game Night Improvvisata quick-start flow.
/// Game Night Improvvisata - E2-1: Start session from PrivateGame.
/// </summary>
public sealed record StartImprovvisataSessionCommand(
    Guid UserId,
    Guid PrivateGameId) : IRequest<StartImprovvisataSessionResponse>;

/// <summary>
/// Response returned after a successful session start.
/// </summary>
public sealed record StartImprovvisataSessionResponse(
    Guid SessionId,
    string InviteCode,
    string ShareLink);

/// <summary>
/// Validates StartImprovvisataSessionCommand inputs.
/// </summary>
public sealed class StartImprovvisataSessionValidator : AbstractValidator<StartImprovvisataSessionCommand>
{
    public StartImprovvisataSessionValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.PrivateGameId)
            .NotEmpty()
            .WithMessage("Private game ID is required");
    }
}

/// <summary>
/// Handles the StartImprovvisataSessionCommand.
/// Creates a LiveGameSession + SessionInvite atomically.
/// </summary>
internal sealed class StartImprovvisataSessionCommandHandler
    : IRequestHandler<StartImprovvisataSessionCommand, StartImprovvisataSessionResponse>
{
    private const int InviteMaxUses = 10;
    private const int InviteExpiryMinutes = 24 * 60; // 24 hours

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartImprovvisataSessionCommandHandler> _logger;

    public StartImprovvisataSessionCommandHandler(
        MeepleAiDbContext dbContext,
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        ILogger<StartImprovvisataSessionCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StartImprovvisataSessionResponse> Handle(
        StartImprovvisataSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load PrivateGame — throws NotFoundException if missing
        var privateGame = await _dbContext.PrivateGames
            .FirstOrDefaultAsync(g => g.Id == request.PrivateGameId && !g.IsDeleted, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PrivateGame", request.PrivateGameId.ToString());

        // 2. Verify current user owns the game
        if (privateGame.OwnerId != request.UserId)
        {
            throw new ForbiddenException(
                $"User {request.UserId} does not own PrivateGame {request.PrivateGameId}");
        }

        // 3. Create LiveGameSession domain object
        var sessionId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: request.UserId,
            gameName: privateGame.Title,
            timeProvider: _timeProvider,
            gameId: null); // PrivateGames are not in the shared catalog

        // 4. Add current user as host/first player
        session.AddPlayer(
            userId: request.UserId,
            displayName: "Host",
            color: PlayerColor.Red,
            timeProvider: _timeProvider,
            role: PlayerRole.Host);

        // 5. Register the session in the in-memory repository (for real-time tracking)
        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

        // 6. Create SessionInvite domain object (24h expiry, max 10 uses)
        var invite = SessionInvite.Create(
            sessionId: sessionId,
            createdByUserId: request.UserId,
            maxUses: InviteMaxUses,
            expiryMinutes: InviteExpiryMinutes);

        // 7. Persist LiveGameSession entity to database
        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = session.Id,
            SessionCode = session.SessionCode,
            GameId = session.GameId,
            GameName = session.GameName,
            ToolkitId = session.ToolkitId,
            CreatedByUserId = session.CreatedByUserId,
            Visibility = (int)session.Visibility,
            GroupId = session.GroupId,
            Status = (int)session.Status,
            CurrentTurnIndex = session.CurrentTurnIndex,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            AgentMode = (int)session.AgentMode,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });

        // 8. Persist SessionInvite entity to database
        _dbContext.SessionInvites.Add(new SessionInviteEntity
        {
            Id = invite.Id,
            SessionId = invite.SessionId,
            CreatedByUserId = invite.CreatedByUserId,
            Pin = invite.Pin,
            LinkToken = invite.LinkToken,
            MaxUses = invite.MaxUses,
            CurrentUses = invite.CurrentUses,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt,
            IsRevoked = invite.IsRevoked
        });

        // 9. Save atomically
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 10. Build share link
        var shareLink = $"/join/{invite.LinkToken}";

        _logger.LogInformation(
            "User {UserId} started improvvisata session {SessionId} from PrivateGame {PrivateGameId} with invite {InvitePin}",
            request.UserId, sessionId, request.PrivateGameId, invite.Pin);

        return new StartImprovvisataSessionResponse(
            SessionId: sessionId,
            InviteCode: invite.Pin,
            ShareLink: shareLink);
    }
}
