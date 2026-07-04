using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles fetching the cross-game diary timeline for a game night.
/// Queries SessionEvents tagged with the game night ID from the SessionTracking BC.
/// Participant-scoped (#2633 C2): 404 if the night is missing, 403 if the caller is neither the
/// organizer nor an RSVP'd player — same predicate as <c>GetGameNightLiveQueryHandler</c>.
/// </summary>
internal sealed class GetGameNightDiaryQueryHandler
    : IQueryHandler<GetGameNightDiaryQuery, GameNightDiaryDto>
{
    // Cap the read so a long night can't return an unbounded payload; newest-first so the truncated
    // window is the RECENT tail (#2633 C2 must-fix), then re-sorted to chronological for render.
    private const int DiaryReadCap = 200;

    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly ISessionEventRepository _sessionEventRepository;

    public GetGameNightDiaryQueryHandler(
        IGameNightEventRepository gameNightRepository,
        ISessionEventRepository sessionEventRepository)
    {
        _gameNightRepository = gameNightRepository
            ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _sessionEventRepository = sessionEventRepository
            ?? throw new ArgumentNullException(nameof(sessionEventRepository));
    }

    public async Task<GameNightDiaryDto> Handle(
        GetGameNightDiaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Participant guard (parity with the live query): load the aggregate to 404 a bogus id and
        // 403 a non-participant — the old handler returned 200+empty for any id (an existence oracle).
        var gameNight = await _gameNightRepository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        var isParticipant = gameNight.OrganizerId == query.CallerUserId
            || gameNight.Rsvps.Any(r => r.UserId == query.CallerUserId);
        if (!isParticipant)
            throw new ForbiddenException("Only the organizer or an invited player can view the night diary.");

        var events = await _sessionEventRepository
            .GetByGameNightIdAsync(query.GameNightId, limit: DiaryReadCap, newestFirst: true, ct: cancellationToken)
            .ConfigureAwait(false);

        var entries = events
            .OrderBy(e => e.Timestamp) // chronological render even though we read the recent tail first
            .Select(e => new GameNightDiaryEntryDto(
                e.Id,
                e.SessionId,
                e.EventType,
                GenerateDescription(e.EventType, e.Payload),
                e.Payload,
                e.CreatedBy,
                e.Timestamp)).ToList();

        return new GameNightDiaryDto(query.GameNightId, entries);
    }

    private static string GenerateDescription(string eventType, string? payload)
    {
        return eventType switch
        {
            "game_started" => "🎲 Partita iniziata",
            "game_completed" => "🏆 Partita completata",
            "night_started" => "🎮 Game Night iniziata",
            "night_finalized" => "📊 Serata completata",
            "score_update" => "📊 Punteggio aggiornato",
            "dice_roll" => "🎲 Dado lanciato",
            "card_draw" => "🃏 Carta pescata",
            "photo" => "📸 Foto aggiunta",
            "pause_resume" => "⏸️ Pausa/Ripresa",
            "player_joined" => "👤 Giocatore entrato",
            "note_added" => "📝 Nota aggiunta",
            "dispute_resolved" => "⚖️ Disputa risolta",
            "resource_update" => "📦 Risorse aggiornate",
            _ => eventType
        };
    }
}
