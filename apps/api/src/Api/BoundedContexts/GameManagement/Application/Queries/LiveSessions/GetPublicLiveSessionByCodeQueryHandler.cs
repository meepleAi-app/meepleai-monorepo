using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Handles <see cref="GetPublicLiveSessionByCodeQuery"/>. Loads the session by code and projects it
/// server-side into the narrow <see cref="PublicLiveSessionDto"/> (NOT the full MapToDto). Returns null
/// for an unknown code so the endpoint maps it to 404. Code-as-capability — no visibility gate. Issue #2590.
/// </summary>
internal sealed class GetPublicLiveSessionByCodeQueryHandler
    : IQueryHandler<GetPublicLiveSessionByCodeQuery, PublicLiveSessionDto?>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetPublicLiveSessionByCodeQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<PublicLiveSessionDto?> Handle(
        GetPublicLiveSessionByCodeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository
            .GetByCodeAsync(query.SessionCode, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return null;
        }

        var players = session.Players
            .Select(p => new PublicLiveSessionPlayerDto(
                p.Id, p.DisplayName, p.Color, p.TotalScore, p.CurrentRank, p.IsActive))
            .ToList();

        return new PublicLiveSessionDto(
            session.Id,
            session.SessionCode,
            session.GameName,
            Slugifier.Slugify(session.GameName),
            session.Status,
            players);
    }
}
