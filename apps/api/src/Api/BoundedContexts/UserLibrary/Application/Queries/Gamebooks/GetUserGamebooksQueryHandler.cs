using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;

/// <summary>
/// Handler for <see cref="GetUserGamebooksQuery"/>.
///
/// Issue #1288 refactor — replaces the original PrivateGame-only lookup
/// (Issue #869 MVP) with a cross-aggregate view that surfaces SharedGame
/// library entries as well, provided they have either:
///   - an active GamebookCampaignSession, OR
///   - an associated PrivateGameId (private rulebook upload)
///
/// Counter aggregation (Pages, Chunks, QaCount, SessionsCount, KB status)
/// remains placeholder here; Bug 2 follow-up wires the
/// IKnowledgeBaseStatsPort + IGamebookCampaignReadPort composition.
/// </summary>
internal sealed class GetUserGamebooksQueryHandler
    : IQueryHandler<GetUserGamebooksQuery, IReadOnlyList<GamebookCardDataDto>>
{
    private readonly IUserGamebookViewRepository _viewRepository;

    public GetUserGamebooksQueryHandler(IUserGamebookViewRepository viewRepository)
    {
        _viewRepository = viewRepository
            ?? throw new ArgumentNullException(nameof(viewRepository));
    }

    public async Task<IReadOnlyList<GamebookCardDataDto>> Handle(
        GetUserGamebooksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entries = await _viewRepository
            .GetGamebookEntriesAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return entries
            .OrderByDescending(e => e.LastActivityAt)
            .Select(MapToDto)
            .ToList();
    }

    private static GamebookCardDataDto MapToDto(UserGamebookViewItem entry) => new(
        Id: entry.LibraryEntryId,
        GameId: entry.GameId,
        Title: entry.Title,
        Publisher: null,
        Year: entry.Year,
        Pages: entry.ReadyPdfCount,
        TotalPages: entry.ReadyPdfCount + entry.IndexingPdfCount + entry.FailedPdfCount,
        Chunks: entry.ChunkCount,
        Status: DeriveStatus(entry),
        Cover: entry.Cover,
        Emoji: null,
        QaCount: 0,
        SessionsCount: entry.SessionsCount,
        ErrorMsg: null);

    /// <summary>
    /// Derives the gamebook status badge value from PDF processing counts.
    ///
    /// Rules (AC-4):
    ///   - "ready"     → at least one PDF in Ready state (KB queryable)
    ///   - "indexing"  → no Ready PDFs but at least one in transition states
    ///   - "error"     → no Ready/Indexing PDFs but at least one Failed
    ///   - "ready"     → fallback (e.g. library entry with no PDFs yet, treated
    ///                   as ready so the card renders without warning UI)
    /// </summary>
    private static string DeriveStatus(UserGamebookViewItem entry)
    {
        if (entry.ReadyPdfCount > 0) return "ready";
        if (entry.IndexingPdfCount > 0) return "indexing";
        if (entry.FailedPdfCount > 0) return "error";
        return "ready";
    }
}
