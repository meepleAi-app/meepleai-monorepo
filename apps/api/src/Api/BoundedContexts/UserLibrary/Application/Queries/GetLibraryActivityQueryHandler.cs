using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for the activity-feed query (Issue #642).
/// Reads <c>UserLibraryEntryEntity</c> rows owned by the user and emits up to
/// two pseudo-events per entry: an <c>added</c> event keyed on <c>AddedAt</c>
/// and (if applicable) a <c>state-changed</c> event keyed on <c>StateChangedAt</c>.
/// Joins <c>SharedGames</c> / <c>PrivateGames</c> only to surface a stable game title.
/// Returns the most recent <c>Limit</c> events ordered by timestamp DESC.
/// </summary>
internal class GetLibraryActivityQueryHandler
    : IQueryHandler<GetLibraryActivityQuery, IReadOnlyList<LibraryActivityItemDto>>
{
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

        // Pull at most (Limit * 2) entries since each entry can yield 1–2 events.
        // Order by the most recent activity timestamp on the entry first so we
        // don't drop a state-changed event from an old entry whose underlying
        // AddedAt is earlier than other entries' AddedAt.
        var rows = await _dbContext.UserLibraryEntries
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

        var events = new List<LibraryActivityItemDto>(rows.Count * 2);
        foreach (var row in rows)
        {
            var gameId = row.SharedGameId ?? row.PrivateGameId ?? Guid.Empty;
            var gameTitle = row.SharedTitle ?? row.PrivateTitle ?? "Unknown";

            // "added" event — every entry has an AddedAt timestamp.
            events.Add(new LibraryActivityItemDto(
                Id: row.Id,
                Type: "added",
                Timestamp: row.AddedAt,
                GameId: gameId,
                GameTitle: gameTitle,
                Message: $"Aggiunto {gameTitle} alla libreria"
            ));

            // "state-changed" event — only when the state was changed after addition.
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

        return events
            .OrderByDescending(e => e.Timestamp)
            .Take(request.Limit)
            .ToList();
    }
}
