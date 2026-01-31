using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for GetSessionStatusQuery with authorization.
/// Verifies that requesting user owns the session OR has Admin role.
/// </summary>
internal class GetSessionStatusQueryHandler : IQueryHandler<GetSessionStatusQuery, SessionInfo?>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetSessionStatusQueryHandler> _logger;

    public GetSessionStatusQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetSessionStatusQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SessionInfo?> Handle(GetSessionStatusQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = await _db.UserSessions
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", request.SessionId);
            return null;
        }

        // Authorization check: User must own the session OR be an admin
        if (session.UserId != request.RequestingUserId && !request.IsRequestingUserAdmin)
        {
            _logger.LogWarning(
                "User {UserId} attempted to access session {SessionId} owned by {OwnerId} without admin privileges",
                request.RequestingUserId, request.SessionId, session.UserId);
            return null;
        }

        return new SessionInfo(
            session.Id.ToString(),
            session.UserId.ToString(),
            session.User.Email,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt,
            session.RevokedAt,
            session.IpAddress,
            session.UserAgent
        );
    }
}
