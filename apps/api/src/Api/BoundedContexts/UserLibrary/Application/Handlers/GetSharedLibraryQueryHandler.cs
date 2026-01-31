using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting a shared library by its share token (public access).
/// Records access and returns library data.
/// </summary>
internal class GetSharedLibraryQueryHandler : IQueryHandler<GetSharedLibraryQuery, SharedLibraryDto?>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetSharedLibraryQueryHandler> _logger;

    public GetSharedLibraryQueryHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db,
        ILogger<GetSharedLibraryQueryHandler> logger)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SharedLibraryDto?> Handle(GetSharedLibraryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var shareLink = await _shareLinkRepository.GetByShareTokenAsync(query.ShareToken, cancellationToken).ConfigureAwait(false);

        if (shareLink == null)
        {
            _logger.LogDebug("Share link not found for token: {Token}", query.ShareToken);
            return null;
        }

        if (!shareLink.IsValid)
        {
            _logger.LogDebug("Share link {LinkId} is invalid (revoked or expired)", shareLink.Id);
            return null;
        }

        // Record access
        shareLink.RecordAccess();
        await _shareLinkRepository.UpdateAsync(shareLink, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Get owner display name
        var owner = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == shareLink.UserId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (owner == null)
        {
            _logger.LogWarning("Owner user {UserId} not found for share link {LinkId}", shareLink.UserId, shareLink.Id);
            return null;
        }

        var ownerDisplayName = !string.IsNullOrWhiteSpace(owner.DisplayName)
            ? owner.DisplayName
            : owner.Email?.Split('@')[0] ?? "Anonymous";

        // Get library entries
        var (entries, totalCount) = await _libraryRepository.GetUserLibraryPaginatedAsync(
            userId: shareLink.UserId,
            search: null,
            favoritesOnly: null,
            stateFilter: null,
            sortBy: "addedAt",
            descending: true,
            page: 1,
            pageSize: 100, // Get all for shared view
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        // Get SharedGame info for each entry
        var gameIds = entries.Select(e => e.GameId).ToList();
        var sharedGames = await _db.SharedGames
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken)
            .ConfigureAwait(false);

        var favoritesCount = entries.Count(e => e.IsFavorite);

        var games = entries.Select(entry =>
        {
            sharedGames.TryGetValue(entry.GameId, out var game);
            return new SharedLibraryGameDto(
                GameId: entry.GameId,
                Title: game?.Title ?? "Unknown Game",
                Publisher: game?.Publishers.FirstOrDefault()?.Name,
                YearPublished: game?.YearPublished,
                IconUrl: game?.ThumbnailUrl,
                ImageUrl: game?.ImageUrl,
                IsFavorite: entry.IsFavorite,
                Notes: shareLink.IncludeNotes ? entry.Notes?.Value : null,
                AddedAt: entry.AddedAt
            );
        }).ToList();

        return new SharedLibraryDto(
            OwnerDisplayName: ownerDisplayName,
            Games: games,
            TotalGames: totalCount,
            FavoritesCount: favoritesCount,
            PrivacyLevel: shareLink.PrivacyLevel.ToString().ToLowerInvariant(),
            SharedAt: shareLink.CreatedAt
        );
    }
}
