// SearchGamesQuery - Multi-source game search
//
// Searches across:
// - UserLibraryEntries (user's library games)
// - SharedGames (public catalog)
// - PrivateGames (user's private games in library)
//
// Issue #4273: Game Search Autocomplete

using System.Globalization;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

public record SearchGamesQuery(
    string Query,
    Guid UserId,
    int MaxResults = 20
) : IRequest<List<GameSearchResultDto>>;

public class SearchGamesQueryHandler(MeepleAiDbContext context)
    : IRequestHandler<SearchGamesQuery, List<GameSearchResultDto>>
{
    public async Task<List<GameSearchResultDto>> Handle(
        SearchGamesQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.Query.Trim().ToLower(CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return [];
        }

        // Search user's library (both shared and private games)
        var libraryGames = await context.UserLibraryEntries
            .Include(e => e.SharedGame)
            .Include(e => e.PrivateGame)
            .Where(e => e.UserId == request.UserId)
            .Where(e =>
                // Shared game
                (e.SharedGame != null &&
                 e.SharedGame.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                // Private game
                (e.PrivateGame != null &&
                 e.PrivateGame.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            )
            .Select(e => new GameSearchResultDto
            {
                Id = e.SharedGameId ?? e.PrivateGameId ?? e.Id, // Use SharedGameId, PrivateGameId, or LibraryEntryId
                Name = e.SharedGame != null
                    ? e.SharedGame.Title
                    : e.PrivateGame!.Title,
                Source = e.PrivateGameId != null ? "private" : "library",
                ImageUrl = e.SharedGame != null
                    ? e.SharedGame.ImageUrl
                    : e.PrivateGame!.ImageUrl
            })
            .Take(request.MaxResults / 2) // Reserve half for catalog
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get library game IDs to exclude from catalog search
        var libraryGameIds = await context.UserLibraryEntries
            .Where(e => e.UserId == request.UserId && e.SharedGameId != null)
            .Select(e => e.SharedGameId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Search shared catalog (exclude games already in user's library)
        var catalogGames = await context.SharedGames
            .Where(g => !g.IsDeleted &&
                        g.Title.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                        !libraryGameIds.Contains(g.Id))
            .Select(g => new GameSearchResultDto
            {
                Id = g.Id,
                Name = g.Title,
                Source = "catalog",
                ImageUrl = g.ImageUrl
            })
            .Take(request.MaxResults / 2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Combine and return (library first, then catalog)
        return [.. libraryGames, .. catalogGames];
    }
}
