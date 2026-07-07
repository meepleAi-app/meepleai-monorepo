using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Projects a <see cref="GameNightEvent"/> aggregate into the <see cref="GameNightSummaryDto"/>
/// read model — Issue #2702. Shared by the participant summary handler and the public
/// share-token handler; the winner-name resolver is supplied by the caller (a cross-BC
/// SessionTracking participant read).
/// </summary>
internal static class GameNightSummaryMapper
{
    public static GameNightSummaryDto Map(
        GameNightEvent night,
        Func<GameNightSession, string?> winnerName,
        IReadOnlyDictionary<Guid, int> eventCountBySession,
        IReadOnlyDictionary<Guid, IReadOnlyList<GameNightRecapPlayerDto>> topPlayersBySession,
        bool isViewerOrganizer)
    {
        var games = night.Sessions
            .OrderBy(s => s.PlayOrder)
            .Select(s => new GameNightGameRecapDto(
                s.SessionId, s.GameId, s.GameTitle, s.PlayOrder, s.Status,
                s.WinnerId, winnerName(s), DurationMinutes(s),
                eventCountBySession.TryGetValue(s.SessionId, out var count) ? count : 0,
                topPlayersBySession.TryGetValue(s.SessionId, out var top)
                    ? top
                    : Array.Empty<GameNightRecapPlayerDto>()))
            .ToList();

        var kpis = new GameNightSummaryKpisDto(
            TotalGames: night.Sessions.Count,
            CompletedGames: night.Sessions.Count(s => s.Status == GameNightSessionStatus.Completed),
            TotalDurationMinutes: night.Sessions.Sum(s => DurationMinutes(s) ?? 0),
            WinnersCount: night.Sessions.Count(s => s.WinnerId is not null));

        // MVP = the winner name with the most session wins (ties resolved by first-seen order).
        var mvp = night.Sessions
            .Where(s => s.WinnerId is not null)
            .Select(winnerName)
            .Where(n => !string.IsNullOrEmpty(n))
            .GroupBy(n => n!, StringComparer.Ordinal)
            .Select(g => new GameNightMvpDto(g.Key, g.Count()))
            .OrderByDescending(m => m.Wins)
            .FirstOrDefault();

        return new GameNightSummaryDto(
            night.Id,
            night.Title,
            night.Status,
            night.ScheduledAt,
            night.Location,
            isViewerOrganizer,
            night.IsArchived,
            night.IsShared,
            isViewerOrganizer ? night.ShareToken : null,
            mvp,
            kpis,
            games);
    }

    private static int? DurationMinutes(GameNightSession s) =>
        s.StartedAt is { } start && s.CompletedAt is { } end && end > start
            ? (int)Math.Round((end - start).TotalMinutes)
            : null;
}
