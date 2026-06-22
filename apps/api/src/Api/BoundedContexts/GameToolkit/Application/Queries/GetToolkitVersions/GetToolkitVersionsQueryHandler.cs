using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;

/// <summary>
/// Handler for <see cref="GetToolkitVersionsQuery"/> — returns the published
/// version history sorted by <c>PublishedAt DESC</c>.
/// Wave 3 Phase 2 + Phase 5 (issue #822 / PR-1 schema foundation).
/// </summary>
/// <remarks>
/// <para>
/// Cache strategy (PR #732 §5.3.2): 10min HybridCache, no per-viewer
/// partition — versions are non-personalised once the toolkit is visible
/// (visibility check still executes per request before cache lookup).
/// </para>
/// <para>
/// Phase 5 schema refresh (issue #822): handler reads real
/// <c>ToolkitVersion</c> rows via <see cref="IToolkitVersionRepository"/>
/// instead of synthesising a stub. The fallback stub
/// (<see cref="BuildStubVersionsList"/>) is retained for the empty-state
/// path: a toolkit with <c>IsPublished=false</c> still has zero rows after
/// the migration backfill, so we surface the "draft only" affordance via
/// the same single-row shape rather than returning <c>{ items: [] }</c>.
/// </para>
/// </remarks>
internal sealed class GetToolkitVersionsQueryHandler
    : IRequestHandler<GetToolkitVersionsQuery, ToolkitVersionsResponse?>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly MeepleAiDbContext _context;
    private readonly IToolkitVersionRepository _versions;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetToolkitVersionsQueryHandler> _logger;

    public GetToolkitVersionsQueryHandler(
        MeepleAiDbContext context,
        IToolkitVersionRepository versions,
        IHybridCacheService cache,
        ILogger<GetToolkitVersionsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _versions = versions ?? throw new ArgumentNullException(nameof(versions));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolkitVersionsResponse?> Handle(
        GetToolkitVersionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Visibility check happens outside the cache so a non-author cannot
        // benefit from a cached author response if the toolkit becomes a draft.
        var entity = await _context.GameToolkits
            .AsNoTracking()
            .Where(t => t.Id == request.ToolkitId)
            .Select(t => new
            {
                t.Id,
                t.CreatedByUserId,
                t.IsPublished,
                t.TemplateStatus,
                t.UpdatedAt,
                t.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} not found for versions request (viewer {ViewerId})",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        var isOwner = entity.CreatedByUserId == request.ViewerId;
        var isPublished = entity.IsPublished
            && (TemplateStatus)entity.TemplateStatus == TemplateStatus.Approved;

        if (!isOwner && !isPublished)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} versions hidden from viewer {ViewerId} (not published, not author)",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        var cacheKey = $"toolkits:{request.ToolkitId:N}:versions";
        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await BuildResponseAsync(
                request.ToolkitId,
                entity.IsPublished,
                entity.UpdatedAt,
                entity.CreatedAt,
                ct).ConfigureAwait(false),
            tags:
            [
                $"toolkit:{request.ToolkitId:N}",
                "toolkitVersions",
            ],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Returning {Count} version(s) for toolkit {ToolkitId} (viewer {ViewerId})",
            cached.Items.Count,
            request.ToolkitId,
            request.ViewerId);

        return cached;
    }

    /// <summary>
    /// Reads the real <c>ToolkitVersion</c> rows (Phase 5 schema, issue #822).
    /// Falls back to the legacy stub only when the toolkit is still a draft
    /// (no version rows exist by design — see migration §backfill rationale).
    /// </summary>
    private async Task<ToolkitVersionsResponse> BuildResponseAsync(
        Guid toolkitId,
        bool isPublished,
        DateTime updatedAt,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        // Include yanked rows so the FE can render the "version was yanked"
        // affordance — the marketplace surface filters by IsCurrent on the
        // client side / via list semantics.
        var versions = await _versions
            .GetByToolkitIdAsync(toolkitId, includeYanked: true, cancellationToken)
            .ConfigureAwait(false);

        if (versions.Count == 0)
        {
            // Empty history — surface the draft / never-published affordance
            // via the legacy single-row shape to keep the FE wire stable.
            return BuildStubVersionsList(
                version: 0,
                updatedAt: updatedAt,
                createdAt: createdAt,
                isPublished: isPublished);
        }

        // Identify the latest non-yanked row for IsCurrent.
        var latestNonYankedId = versions
            .Where(v => !v.IsYanked)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefault();

        var items = versions
            .Select(v => new ToolkitVersionDto(
                Version: v.VersionNumber,
                PublishedAt: v.PublishedAt,
                YankedAt: v.YankedAt,
                Changelog: v.Changelog ?? string.Empty,
                IsCurrent: latestNonYankedId.HasValue && v.Id == latestNonYankedId.Value))
            .ToList();

        return new ToolkitVersionsResponse(items);
    }

    /// <summary>
    /// Legacy stub builder retained for draft / pre-publish toolkits where
    /// the version-history table has no rows yet. PR-2 (#822 endpoints) will
    /// keep using this exact shape for the draft state.
    /// </summary>
    internal static ToolkitVersionsResponse BuildStubVersionsList(
        int version,
        DateTime updatedAt,
        DateTime createdAt,
        bool isPublished)
    {
        var publishedAt = isPublished ? updatedAt : createdAt;
        var changelog = isPublished
            ? "Initial version"
            : "Draft — not yet published";

        var items = new List<ToolkitVersionDto>
        {
            new(
                Version: $"1.0.{version}",
                PublishedAt: publishedAt,
                YankedAt: null,
                Changelog: changelog,
                IsCurrent: true),
        };

        return new ToolkitVersionsResponse(items);
    }
}
