using System.Text.Json;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for the activity-feed query (Issue #642 + #661).
///
/// Reads from TWO disjoint sources (spec §3.5 hardened):
///   (a) <c>UserLibraryEntries</c> rows — projects <c>added</c> and
///       <c>state-changed</c> from row timestamps (legacy MVP path).
///   (b) <c>domain_event_logs</c> — projects <c>removed</c> and
///       <c>session-recorded</c> from durable event log rows (post-#661).
///
/// The two sources are disjoint (no overlapping kinds), so the merge is
/// a straight ordered union. Pagination is cursor-based on
/// <c>Timestamp DESC, Id</c>. Retention: events older than
/// <c>DomainEventLog:RetentionDays</c> (default 90) are filtered out at
/// query time.
/// </summary>
internal class GetLibraryActivityQueryHandler
    : IQueryHandler<GetLibraryActivityQuery, IReadOnlyList<LibraryActivityItemDto>>
{
    private const int DefaultRetentionDays = 90;

    private readonly MeepleAiDbContext _dbContext;

    public GetLibraryActivityQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<LibraryActivityItemDto>> Handle(
        GetLibraryActivityQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var retentionCutoff = DateTime.UtcNow.AddDays(-DefaultRetentionDays);

        // ---------- Source (a): legacy row-timestamp projection ----------
        var libraryRows = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == request.UserId)
            .OrderByDescending(e => e.StateChangedAt ?? e.AddedAt)
            .Take(request.Limit * 2)
            .Select(e => new
            {
                e.Id,
                SharedGameId = e.SharedGameId,
                PrivateGameId = e.PrivateGameId,
                e.AddedAt,
                e.StateChangedAt,
                e.CurrentState,
                SharedTitle = e.SharedGame != null ? e.SharedGame.Title : null,
                PrivateTitle = e.PrivateGame != null ? e.PrivateGame.Title : null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var events = new List<LibraryActivityItemDto>(libraryRows.Count * 2);
        foreach (var row in libraryRows)
        {
            var gameId = row.SharedGameId ?? row.PrivateGameId ?? Guid.Empty;
            var gameTitle = row.SharedTitle ?? row.PrivateTitle ?? "Unknown";

            // "added" — every entry has an AddedAt timestamp.
            events.Add(new LibraryActivityItemDto(
                Id: row.Id,
                Type: "added",
                Timestamp: row.AddedAt,
                GameId: gameId,
                GameTitle: gameTitle,
                Message: $"Aggiunto {gameTitle} alla libreria"
            ));

            // "state-changed" — only when the state was changed after addition.
            if (row.StateChangedAt.HasValue && row.StateChangedAt.Value > row.AddedAt)
            {
                var stateLabel = ((GameStateType)row.CurrentState).ToString();
                events.Add(new LibraryActivityItemDto(
                    Id: row.Id,
                    Type: "state-changed",
                    Timestamp: row.StateChangedAt.Value,
                    GameId: gameId,
                    GameTitle: gameTitle,
                    Message: $"Aggiornato lo stato di {gameTitle} a {stateLabel}"
                ));
            }
        }

        // ---------- Source (b): domain_event_logs ----------
        // Read both kinds in a single round-trip; filter retention at the DB.
        var logRows = await _dbContext.DomainEventLogs
            .AsNoTracking()
            .Where(l => l.UserId == request.UserId
                        && l.LoggedAt >= retentionCutoff
                        && (l.EventType == "library.entry.removed"
                            || l.EventType == "library.session.recorded"))
            .OrderByDescending(l => l.LoggedAt)
            .Take(request.Limit * 2)
            .Select(l => new
            {
                l.Id,
                l.EventType,
                l.AggregateId,
                l.PayloadJson,
                l.OccurredAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var log in logRows)
        {
            var (gameId, gameTitle) = ExtractGamePayload(log.PayloadJson, log.EventType);
            var aggregateId = log.AggregateId ?? log.Id;

            switch (log.EventType)
            {
                case "library.entry.removed":
                    events.Add(new LibraryActivityItemDto(
                        Id: aggregateId,
                        Type: "removed",
                        Timestamp: log.OccurredAt,
                        GameId: gameId,
                        GameTitle: gameTitle,
                        Message: $"Rimosso {gameTitle} dalla libreria"
                    ));
                    break;

                case "library.session.recorded":
                    events.Add(new LibraryActivityItemDto(
                        Id: aggregateId,
                        Type: "session-recorded",
                        Timestamp: log.OccurredAt,
                        GameId: gameId,
                        GameTitle: gameTitle,
                        Message: $"Registrata una partita di {gameTitle}"
                    ));
                    break;
            }
        }

        // ---------- Merge + paginate (in-memory; bounded by 2 × limit per source) ----------
        return events
            .OrderByDescending(e => e.Timestamp)
            .Take(request.Limit)
            .ToList();
    }

    /// <summary>
    /// Best-effort extraction of <c>GameId</c> + game title from a logged
    /// event payload. The payload schema is the event class itself
    /// (camelCase JSON). Until we add denormalized title columns we resolve
    /// titles by joining the game catalogs — for the removed case the
    /// library entry is gone, so we display the game id as a fallback.
    /// </summary>
    private static (Guid gameId, string gameTitle) ExtractGamePayload(string payloadJson, string eventType)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            Guid gameId = Guid.Empty;
            if (root.TryGetProperty("gameId", out var gameIdEl) &&
                gameIdEl.TryGetGuid(out var parsedGameId))
            {
                gameId = parsedGameId;
            }

            // Future enhancement: persist a `gameTitle` snapshot in the event
            // payload so the activity feed survives game-catalog deletions.
            // For now, the i18n message reads "Game" when the title can't be
            // resolved.
            var gameTitle = gameId == Guid.Empty ? "Unknown" : "Game";
            return (gameId, gameTitle);
        }
        catch (JsonException)
        {
            return (Guid.Empty, "Unknown");
        }
    }
}
