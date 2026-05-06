using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;

/// <summary>
/// Handler for <see cref="GetToolkitVersionsQuery"/> — returns the published
/// version history sorted by <c>PublishedAt DESC</c>.
/// Wave 3 Phase 2, PR #732 §5.3.2 / Issue #805.
/// </summary>
/// <remarks>
/// Cache strategy (PR #732 §5.3.2): 10min HybridCache, no per-viewer
/// partition — versions are non-personalised once the toolkit is visible
/// (visibility check still executes per request before cache lookup).
///
/// Schema reality v1 carryover (Gate B): no <c>ToolkitVersion</c> entity.
/// The handler synthesises a single-row stub: <c>{ version: "1.0.{int}",
/// publishedAt: UpdatedAt or CreatedAt, yankedAt: null, changelog:
/// "Initial version", isCurrent: true }</c>. Wire shape is stable so the
/// FE versions list can render today and absorb the real history rows
/// (sorted desc, isCurrent on the latest row) without a fetch shape change.
/// </remarks>
internal sealed class GetToolkitVersionsQueryHandler
    : IRequestHandler<GetToolkitVersionsQuery, ToolkitVersionsResponse?>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetToolkitVersionsQueryHandler> _logger;

    public GetToolkitVersionsQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetToolkitVersionsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
                t.Version,
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
            ct => Task.FromResult(BuildStubVersionsList(entity.Version, entity.UpdatedAt, entity.CreatedAt, isPublished)),
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
    /// Synthesises the v1 stub versions list. When the toolkit has not yet
    /// been published (<c>IsPublished == false</c>), the handler still returns
    /// a single-row entry so the FE can render the "draft only" state without
    /// special-casing — <c>publishedAt</c> falls back to <c>CreatedAt</c>.
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
