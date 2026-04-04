using Api.BoundedContexts.UserLibrary.Application.Queries.Games;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Games;

/// <summary>
/// Handler for GetUserGamesQuery (Issue #4580)
/// Retrieves user's game collection with filters, sorting, and pagination
/// </summary>
internal class GetUserGamesQueryHandler : IRequestHandler<GetUserGamesQuery, PagedResult<UserGameDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetUserGamesQueryHandler(
        MeepleAiDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task<PagedResult<UserGameDto>> Handle(GetUserGamesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = GetCurrentUserId();

        // Build base query
        var query = _context.Set<UserLibraryEntryEntity>()
            .Where(e => e.UserId == userId)
            .Include(e => e.SharedGame)
            .Include(e => e.PrivateGame)
            .AsQueryable();

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply sorting
        query = request.Sort switch
        {
            "alphabetical" => query.OrderBy(e => e.SharedGame != null ? e.SharedGame.Title : (e.PrivateGame != null ? e.PrivateGame.Title : string.Empty)),
            "playCount" => query.OrderByDescending(e => e.TimesPlayed),
            _ => query.OrderBy(e => e.SharedGame != null ? e.SharedGame.Title : string.Empty)
        };

        // Apply pagination
        var entries = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Map to DTOs (outside of expression tree to avoid CS0853)
        var items = entries.Select(e =>
        {
            // Can't use ?? with different types - check explicitly
            if (e.SharedGame == null && e.PrivateGame == null)
            {
                return null;
            }

            return new UserGameDto(
                e.SharedGameId ?? e.PrivateGameId ?? e.Id,
                e.SharedGame?.Title ?? e.PrivateGame!.Title,
                null,
                e.SharedGame?.ImageUrl ?? e.PrivateGame?.ImageUrl,
                e.SharedGame?.ThumbnailUrl ?? e.PrivateGame?.ThumbnailUrl,
                e.SharedGame?.AverageRating,
                e.SharedGame?.MinPlayers ?? e.PrivateGame?.MinPlayers,
                e.SharedGame?.MaxPlayers ?? e.PrivateGame?.MaxPlayers,
                e.SharedGame?.PlayingTimeMinutes ?? e.PrivateGame?.PlayingTimeMinutes,
                e.SharedGame?.ComplexityRating,
                e.TimesPlayed,
                null, // LastPlayed - computed separately if needed
                true,
                false
            );
        })
        .Where(dto => dto != null)
        .Cast<UserGameDto>()  // Cast to non-nullable after filtering
        .ToList();

        return new PagedResult<UserGameDto>(
            items,
            totalCount,
            request.Page,
            request.PageSize);
    }

    private Guid GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context available");

        var userIdClaim = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        return Guid.Parse(userIdClaim);
    }
}
