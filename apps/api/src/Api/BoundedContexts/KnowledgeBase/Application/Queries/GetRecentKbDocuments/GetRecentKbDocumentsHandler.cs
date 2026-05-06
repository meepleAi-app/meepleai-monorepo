using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;

/// <summary>
/// Returns public, Ready-state KB documents ordered by IndexedAt DESC.
/// Joins VectorDocument → SharedGame (via VectorDocument.SharedGameId) to resolve GameName.
/// Language comes from PdfDocumentEntity.Language.
/// Limit is clamped to [1, 20].
/// Issue #728: Discover dashboard — "Recent KB Documents" widget.
/// </summary>
internal sealed class GetRecentKbDocumentsHandler : IQueryHandler<GetRecentKbDocumentsQuery, IReadOnlyList<RecentKbDocDto>>
{
    private const int MaxLimit = 20;
    private const int MinLimit = 1;
    private const string ReadyState = "Ready";

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRecentKbDocumentsHandler> _logger;

    public GetRecentKbDocumentsHandler(MeepleAiDbContext dbContext, ILogger<GetRecentKbDocumentsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RecentKbDocDto>> Handle(GetRecentKbDocumentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, MinLimit, MaxLimit);

        _logger.LogInformation("Fetching top {Limit} recent KB documents ordered by IndexedAt DESC", limit);

        return await (
            from pdf in _dbContext.PdfDocuments.AsNoTracking()
            join vd in _dbContext.VectorDocuments.AsNoTracking()
                on pdf.Id equals vd.PdfDocumentId
            join game in _dbContext.SharedGames.AsNoTracking()
                on vd.SharedGameId equals game.Id into gameJoin
            from game in gameJoin.DefaultIfEmpty()
            where pdf.IsPublic
                  && pdf.ProcessingState == ReadyState
                  && vd.IndexedAt != null
            orderby vd.IndexedAt descending
            select new RecentKbDocDto(
                pdf.Id,
                pdf.FileName,
                game != null ? game.Title : string.Empty,
                pdf.DocumentCategory,
                vd.IndexedAt!.Value,
                pdf.Language
            )
        ).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
