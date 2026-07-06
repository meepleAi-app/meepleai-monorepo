using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles <see cref="GetGameNightSummaryQuery"/>: participant-scoped read (#2698) that composes
/// the MVP + KPI + per-game recap via <see cref="GameNightSummaryMapper"/>. Winner names come from
/// a cross-BC SessionTracking participant read scoped to this night's sessions (parity with the
/// live handler).
/// </summary>
internal sealed class GetGameNightSummaryQueryHandler
    : IQueryHandler<GetGameNightSummaryQuery, GameNightSummaryDto>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _db;

    public GetGameNightSummaryQueryHandler(IGameNightEventRepository repository, MeepleAiDbContext db)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<GameNightSummaryDto> Handle(
        GetGameNightSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        var isOrganizer = gameNight.OrganizerId == query.CallerUserId;
        var isParticipant = isOrganizer || gameNight.Rsvps.Any(r => r.UserId == query.CallerUserId);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view the summary.");

        var winnerName = await GameNightSummaryWinnerResolver
            .BuildAsync(_db, gameNight, cancellationToken).ConfigureAwait(false);

        return GameNightSummaryMapper.Map(gameNight, winnerName, isOrganizer);
    }
}
