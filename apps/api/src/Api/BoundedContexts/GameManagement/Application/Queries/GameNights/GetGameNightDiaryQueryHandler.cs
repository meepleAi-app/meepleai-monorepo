using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles fetching the cross-game diary timeline for a game night.
/// Queries SessionEvents tagged with the game night ID from the SessionTracking BC.
/// </summary>
internal sealed class GetGameNightDiaryQueryHandler
    : IQueryHandler<GetGameNightDiaryQuery, GameNightDiaryDto>
{
    private readonly ISessionEventRepository _sessionEventRepository;

    public GetGameNightDiaryQueryHandler(ISessionEventRepository sessionEventRepository)
    {
        _sessionEventRepository = sessionEventRepository
            ?? throw new ArgumentNullException(nameof(sessionEventRepository));
    }

    public async Task<GameNightDiaryDto> Handle(
        GetGameNightDiaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var events = await _sessionEventRepository
            .GetByGameNightIdAsync(query.GameNightId, ct: cancellationToken)
            .ConfigureAwait(false);

        var entries = events.Select(e => new GameNightDiaryEntryDto(
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
