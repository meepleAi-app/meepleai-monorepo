using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Handler for <see cref="GetKbDocumentByIdQuery"/> — Wave 3 Phase 3 spec-conformant
/// rewrite of <c>GET /api/v1/kb-docs/{id}</c> per PR #732 §6.3.1 verbatim.
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantics</b>:
/// <list type="bullet">
///   <item>200 OK + full DTO when <c>processingStatus == 'ready'</c>.</item>
///   <item>404 Not Found when document is missing.</item>
///   <item>403 Forbidden when private doc + non-owner non-admin viewer.</item>
///   <item>423 Locked when <c>processingStatus != 'ready'</c> — distinct from 404
///         per Nygard's operational note. The exception payload still includes
///         partial document metadata for the FE "in elaborazione" render path.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Caching</b>: 1h HybridCache keyed by <c>kb:doc:{docId}:{viewerId}</c>.
/// Per-viewer because the access-control gate may produce 403 for one viewer
/// but 200 for another. Tags <c>["kb", "kbDoc:{id}"]</c> for invalidation when
/// chunks are re-indexed.
/// </para>
///
/// <para>
/// <b>DocType / ProcessingStatus mapping</b>: see <see cref="MapDocType"/> and
/// <see cref="MapProcessingStatus"/>. P21 reuse from
/// <c>GetRecentKbDocsQueryHandler</c> for docType, extended with the 8-state
/// processingStatus collapse.
/// </para>
/// </remarks>
internal sealed class GetKbDocumentByIdHandler : IQueryHandler<GetKbDocumentByIdQuery, KbDocumentDto>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly MeepleAiDbContext _dbContext;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetKbDocumentByIdHandler> _logger;

    public GetKbDocumentByIdHandler(
        MeepleAiDbContext dbContext,
        ISharedGameRepository sharedGameRepository,
        IHybridCacheService cache,
        ILogger<GetKbDocumentByIdHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbDocumentDto> Handle(GetKbDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug("Fetching KB document {DocId} (admin={IsAdmin})", query.DocumentId, query.UserIsAdmin);

        var cacheKey = $"kb:doc:{query.DocumentId:N}:{query.RequestingUserId:N}:{(query.UserIsAdmin ? "a" : "u")}";

        // Wrap in container so HybridCache's class constraint is satisfied for
        // the value channel — the wrapper carries either the DTO or a flag for
        // 423 Locked semantics so we don't lose it across the cache boundary.
        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(query, ct).ConfigureAwait(false),
            tags: ["kb", $"kbDoc:{query.DocumentId:N}"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        if (cached.Locked)
        {
            // Throw outside the factory so 423 is not cached as success.
            // (The cache stored the locked-state DTO snapshot to avoid a re-fetch
            // if another request hits the same key while still locked.)
            throw new LockedException(
                $"KB document {query.DocumentId} is in processing state '{cached.Dto?.ProcessingStatus}' — not yet ready.");
        }

        return cached.Dto!;
    }

    private async Task<KbDocResultContainer> ComputeAsync(
        GetKbDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var data = await (
            from pdf in _dbContext.PdfDocuments.AsNoTracking()
            join vd in _dbContext.VectorDocuments.AsNoTracking()
                on pdf.Id equals vd.PdfDocumentId into vdj
            from vd in vdj.DefaultIfEmpty()
            where pdf.Id == query.DocumentId
            select new
            {
                pdf,
                ChunkCount = vd != null ? vd.ChunkCount : 0,
                IndexedAt = vd != null ? vd.IndexedAt : (DateTime?)null,
                JoinedGameId = vd != null ? vd.GameId : (Guid?)null
            }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            throw new NotFoundException($"KB document {query.DocumentId} not found");
        }

        // Access control — private doc, non-owner, non-admin viewer → 403.
        if (!data.pdf.IsPublic
            && data.pdf.UploadedByUserId != query.RequestingUserId
            && !query.UserIsAdmin)
        {
            throw new ForbiddenException($"Access denied to document {query.DocumentId}");
        }

        // Uploader join — DisplayName preferred, email fallback when missing.
        var uploader = await _dbContext.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => u.Id == data.pdf.UploadedByUserId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var uploaderName = !string.IsNullOrWhiteSpace(uploader?.DisplayName)
            ? uploader!.DisplayName!
            : !string.IsNullOrWhiteSpace(uploader?.Email)
                ? uploader.Email
                : "Unknown uploader";

        // Game name join — prefer SharedGameId (post-ingest canonical) over the
        // VectorDocument-derived GameId (legacy join). Falls back to null when
        // the doc isn't game-scoped.
        var resolvedGameId = data.pdf.SharedGameId ?? data.JoinedGameId;
        string? gameName = null;
        if (resolvedGameId.HasValue)
        {
            var gameNames = await _sharedGameRepository
                .GetNamesByIdsAsync(new[] { resolvedGameId.Value }, cancellationToken)
                .ConfigureAwait(false);
            gameNames.TryGetValue(resolvedGameId.Value, out gameName);
        }

        var lastIngestedAt = data.IndexedAt ?? data.pdf.ProcessedAt ?? data.pdf.UploadedAt;
        var processingStatus = MapProcessingStatus(data.pdf.ProcessingState);

        var dto = new KbDocumentDto(
            Id: data.pdf.Id,
            Title: DeriveTitle(data.pdf.FileName),
            DocType: MapDocType(data.pdf.DocumentCategory),
            GameId: resolvedGameId,
            GameName: gameName,
            UploaderName: uploaderName,
            UploadedAt: data.pdf.UploadedAt,
            LastIngestedAt: lastIngestedAt,
            ProcessingStatus: processingStatus,
            ChunkCount: data.ChunkCount,
            PageCount: data.pdf.PageCount,
            Language: string.IsNullOrWhiteSpace(data.pdf.Language) ? "it" : data.pdf.Language,
            // Gate B v1 carryover — PdfDocumentEntity has no Tags column yet.
            Tags: Array.Empty<string>()
        );

        return new KbDocResultContainer(
            Dto: dto,
            Locked: !string.Equals(processingStatus, "ready", StringComparison.Ordinal));
    }

    /// <summary>
    /// Strips trailing <c>.pdf</c> extension (case-insensitive) so the FE can
    /// render a clean title. Mirrors <c>GetRecentKbDocsQueryHandler.DeriveTitle</c>.
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
    /// Collapses 7-value <c>DocumentCategory</c> enum → 4-value FE wire vocab.
    /// P21 reuse from <c>GetRecentKbDocsQueryHandler.MapDocType</c>.
    /// </summary>
    private static string MapDocType(string documentCategory) => documentCategory switch
    {
        "Rulebook" => "rulebook",
        "Errata" => "errata",
        // Expansion, QuickStart, Reference, PlayerAid, Other → "guide"
        _ => "guide"
    };

    /// <summary>
    /// Collapses 8-state internal <c>PdfProcessingState</c> (Pending, Uploading,
    /// Extracting, Chunking, Embedding, Indexing, Ready, Failed) into the
    /// 4-value FE wire vocabulary <c>{queued, processing, ready, failed}</c>
    /// per PR #732 §6.3.1 spec.
    /// </summary>
    private static string MapProcessingStatus(string processingState) => processingState switch
    {
        "Ready" => "ready",
        "Failed" => "failed",
        "Pending" or "Uploading" => "queued",
        // Extracting, Chunking, Embedding, Indexing → "processing"
        _ => "processing"
    };

    /// <summary>
    /// Cache value container — also carries the 423 Locked flag so we don't
    /// lose semantics across the cache boundary. The DTO is non-null in both
    /// success and locked cases; the flag controls whether the handler should
    /// throw <see cref="LockedException"/> after retrieval.
    /// </summary>
    internal sealed record KbDocResultContainer(KbDocumentDto Dto, bool Locked);
}
