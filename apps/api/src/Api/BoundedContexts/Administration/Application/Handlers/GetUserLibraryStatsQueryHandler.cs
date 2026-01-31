using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetUserLibraryStatsQuery.
/// Issue #3139 - Retrieves user library statistics by querying UserLibrary tables directly.
/// Uses cross-BC query pattern (Administration BC accesses UserLibrary data via DbContext).
/// </summary>
internal sealed class GetUserLibraryStatsQueryHandler
    : IRequestHandler<GetUserLibraryStatsQuery, AdminUserLibraryStatsDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetUserLibraryStatsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Handles the query execution.
    /// Returns null if user has no library entries (interpreted as 404 at endpoint).
    /// </summary>
    public async Task<AdminUserLibraryStatsDto?> Handle(
        GetUserLibraryStatsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Query 1: Check if user has any library entries
        var hasEntries = await _dbContext.Set<Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>()
            .AsNoTracking()
            .AnyAsync(e => e.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!hasEntries)
        {
            return null; // User has no library entries → 404
        }

        // Query 2: Total games count
        var totalGames = await _dbContext.Set<Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Query 3: Favorite games count
        var favoriteGames = await _dbContext.Set<Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == request.UserId && e.IsFavorite, cancellationToken)
            .ConfigureAwait(false);

        // Query 4: Sessions played count (join through UserLibraryEntry)
        var sessionsPlayed = await _dbContext.Set<Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>()
            .AsNoTracking()
            .Where(e => e.UserId == request.UserId)
            .SelectMany(e => e.Sessions)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // Query 5: Date range (oldest and newest added)
        var dateRange = await _dbContext.Set<Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>()
            .AsNoTracking()
            .Where(e => e.UserId == request.UserId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OldestAddedAt = g.Min(e => e.AddedAt),
                NewestAddedAt = g.Max(e => e.AddedAt)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminUserLibraryStatsDto(
            TotalGames: totalGames,
            FavoriteGames: favoriteGames,
            SessionsPlayed: sessionsPlayed,
            OldestAddedAt: dateRange?.OldestAddedAt,
            NewestAddedAt: dateRange?.NewestAddedAt
        );
    }
}
