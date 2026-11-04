using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for getting a shared library by its share token (public access).
/// Records access and returns library data.
/// </summary>
/// <remarks>
/// La visita non salva l'aggregato (#4558). Prima incrementava il contatore sull'aggregato letto e lo
/// riscriveva tutto: una revoca arrivata fra la lettura e la scrittura tornava <c>revoked_at = NULL</c>, e il
/// link revocato era di nuovo pubblico. Ora la visita conta sé stessa con
/// <see cref="ILibraryShareLinkRepository.TryRecordAccessAsync"/>, che controlla la validità e incrementa
/// nello stesso statement. Se il link non è più valido in quel momento la visita risponde 404, anche se
/// la lettura l'aveva trovato valido.
/// </remarks>
internal class GetSharedLibraryQueryHandler : IQueryHandler<GetSharedLibraryQuery, SharedLibraryDto?>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetSharedLibraryQueryHandler> _logger;

    public GetSharedLibraryQueryHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IUserLibraryRepository libraryRepository,
        MeepleAiDbContext db,
        ILogger<GetSharedLibraryQueryHandler> logger)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SharedLibraryDto?> Handle(GetSharedLibraryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var shareLink = await _shareLinkRepository.GetByShareTokenAsync(query.ShareToken, cancellationToken).ConfigureAwait(false);

        if (shareLink == null)
        {
            _logger.LogDebug("Share link not found for token: {Token}", DataMasking.MaskString(query.ShareToken, 8));
            return null;
        }

        if (!shareLink.IsValid)
        {
            _logger.LogDebug("Share link {LinkId} is invalid (revoked or expired)", shareLink.Id);
            return null;
        }

        // Conta la visita solo se il link è ancora valido adesso: fra la lettura e qui il proprietario può
        // averlo revocato, o sostituito con uno nuovo (#4558).
        if (!await _shareLinkRepository.TryRecordAccessAsync(shareLink.Id, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug("Share link {LinkId} became invalid while being visited (revoked or expired)", shareLink.Id);
            return null;
        }

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

        // Every library entry, page by page (#4187): one page of 100 "for the shared view" showed
        // at most 100 games while the response declared the real total.
        var (entries, totalCount) = await _libraryRepository.GetAllUserLibraryEntriesAsync(
            shareLink.UserId,
            sortBy: "addedAt",
            descending: true,
            cancellationToken
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
