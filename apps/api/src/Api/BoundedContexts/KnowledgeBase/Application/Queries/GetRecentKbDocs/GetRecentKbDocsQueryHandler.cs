using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocs;

/// <summary>
/// Handler for <see cref="GetRecentKbDocsQuery"/> — returns the most recently
/// indexed knowledge-base PDF documents (canonical "Ready" state) sorted by
/// <c>ProcessedAt DESC</c> with a fallback to <c>UploadedAt DESC</c>.
/// Wave 3 Phase 1, PR #732 §4.3.5 / Issue #805.
/// </summary>
/// <remarks>
/// <para><b>Filter</b>: <c>ProcessingState == "Ready"</c> excludes in-flight
/// (Pending/Uploading/.../Failed) ingests. The terminal-success state matches
/// the spec's <c>processingStatus == 'ready'</c> contract.</para>
///
/// <para><b>Cache</b>: Redis 5min via HybridCache (PR #732 §3.2 caching matrix).
/// Tags <c>["kb", "discover", "recentDocs"]</c> for batch invalidation when a
/// new doc enters the Ready state.</para>
///
/// <para><b>Cross-BC read</b>: persistence model lives in DocumentProcessing
/// (<see cref="PdfDocumentEntity"/>) but the query is owned by the KnowledgeBase
/// BC because /discover is a KB-product surface. Bulk-fetches game names from
/// SharedGameCatalog for the ordered slice (no N+1).</para>
/// </remarks>
internal sealed class GetRecentKbDocsQueryHandler
    : IRequestHandler<GetRecentKbDocsQuery, IReadOnlyList<RecentKbDocDto>>
{
    private const string CacheKey = "discover:recentKbDocs";
    private const int MaxCacheLimit = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // PdfDocumentEntity.ProcessingState is persisted as a string. Match the
    // canonical "Ready" terminal-success token (see PdfProcessingState enum
    // in DocumentProcessing.Domain.Enums).
    private const string ReadyState = "Ready";

    private readonly MeepleAiDbContext _context;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetRecentKbDocsQueryHandler> _logger;

    public GetRecentKbDocsQueryHandler(
        MeepleAiDbContext context,
        ISharedGameRepository sharedGameRepository,
        IHybridCacheService cache,
        ILogger<GetRecentKbDocsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RecentKbDocDto>> Handle(
        GetRecentKbDocsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allRecent = await _cache.GetOrCreateAsync(
            CacheKey,
            async ct => await ComputeRecentDocsAsync(MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["kb", "discover", "recentDocs"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        var trimmed = allRecent.Take(request.Limit).ToArray();

        _logger.LogInformation(
            "Returning {Count} recent KB docs (limit={Limit}) from cache/compute",
            trimmed.Length,
            request.Limit);

        return trimmed;
    }

    private async Task<List<RecentKbDocDto>> ComputeRecentDocsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        // Read PdfDocumentEntity rows that have completed ingestion. We project
        // the chunk count from VectorDocuments (one-to-one for the indexed slice)
        // via a left-join LINQ subquery so a missing VectorDocument row degrades
        // gracefully to chunkCount=0 rather than excluding the doc.
        var rows = await _context.Set<PdfDocumentEntity>()
            .AsNoTracking()
            .Where(p => p.ProcessingState == ReadyState)
            .OrderByDescending(p => p.ProcessedAt ?? p.UploadedAt)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.SharedGameId,
                p.DocumentCategory,
                IngestedAt = p.ProcessedAt ?? p.UploadedAt,
                ChunkCount = _context.Set<VectorDocumentEntity>()
                    .Where(v => v.PdfDocumentId == p.Id)
                    .Select(v => (int?)v.ChunkCount)
                    .FirstOrDefault() ?? 0
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameIds = rows
            .Where(r => r.SharedGameId.HasValue)
            .Select(r => r.SharedGameId!.Value)
            .Distinct()
            .ToList();

        var gameNames = gameIds.Count > 0
            ? await _sharedGameRepository
                .GetNamesByIdsAsync(gameIds, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return rows.Select(r =>
        {
            var gameName = r.SharedGameId.HasValue
                && gameNames.TryGetValue(r.SharedGameId.Value, out var name)
                    ? name
                    : null;

            return new RecentKbDocDto(
                Id: r.Id,
                Title: DeriveTitle(r.FileName),
                GameId: r.SharedGameId,
                GameName: gameName,
                DocType: MapDocType(r.DocumentCategory),
                LastIngestedAt: r.IngestedAt,
                ChunkCount: r.ChunkCount);
        }).ToList();
    }

    /// <summary>
    /// Strips the trailing <c>.pdf</c> extension (case-insensitive) so the FE
    /// can render a clean title without an additional transform. Falls back to
    /// the raw filename when no extension is present.
    /// </summary>
    private static string DeriveTitle(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        const string pdfExt = ".pdf";
        return fileName.EndsWith(pdfExt, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^pdfExt.Length]
            : fileName;
    }

    /// <summary>
    /// Collapses the 7-value <see cref="DocumentProcessing.Domain.Enums.DocumentCategory"/>
    /// enum into the 4-value FE wire vocabulary documented on
    /// <see cref="RecentKbDocDto"/>.
    /// </summary>
    private static string MapDocType(string documentCategory) => documentCategory switch
    {
        "Rulebook" => "rulebook",
        "Errata" => "errata",
        // Expansion, QuickStart, Reference, PlayerAid, Other → "guide"
        _ => "guide"
    };
}
