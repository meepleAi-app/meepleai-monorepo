using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles the anonymous shared-summary read (#2702). The token is a cryptographic secret —
/// possession authorises the read — so there is no participant/organiser check. A revoked or
/// unknown token resolves to null → 404. The projected summary never re-exposes the token
/// (<c>isViewerOrganizer: false</c>).
/// </summary>
internal sealed class GetGameNightSummaryByShareTokenQueryHandler
    : IQueryHandler<GetGameNightSummaryByShareTokenQuery, GameNightSummaryDto>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _db;

    public GetGameNightSummaryByShareTokenQueryHandler(
        IGameNightEventRepository repository, MeepleAiDbContext db)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<GameNightSummaryDto> Handle(
        GetGameNightSummaryByShareTokenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByShareTokenAsync(query.ShareToken, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGameNight", query.ShareToken);

        var winnerName = await GameNightSummaryWinnerResolver
            .BuildAsync(_db, gameNight, cancellationToken).ConfigureAwait(false);

        return GameNightSummaryMapper.Map(gameNight, winnerName, isViewerOrganizer: false);
    }
}
