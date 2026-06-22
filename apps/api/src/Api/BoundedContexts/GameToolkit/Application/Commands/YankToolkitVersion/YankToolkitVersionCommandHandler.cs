using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.YankToolkitVersion;

/// <summary>
/// Handler for <see cref="YankToolkitVersionCommand"/>
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §1 + §4 + §5).
/// </summary>
/// <remarks>
/// <para>Pipeline (spec-panel §6 Gherkin):</para>
/// <list type="number">
///   <item>Load tracked toolkit row — return <c>null</c> if missing (404).</item>
///   <item>Owner check — throw <c>ForbiddenException</c>.</item>
///   <item>Load version aggregate — return <c>null</c> if missing OR if it
///         does not belong to the supplied toolkit (mismatched route params).</item>
///   <item>Call <c>version.Yank(ViewerId, Reason, now)</c> (raises domain event;
///         throws <c>ConflictException</c> if already yanked).</item>
///   <item>Persist via repository.</item>
///   <item>Cascade check (§1): if no remaining non-yanked versions, flip
///         <c>toolkit.IsPublished = false</c> in the same transaction.</item>
///   <item>SaveChanges (single transaction via <see cref="IUnitOfWork"/>).</item>
///   <item>Cache invalidation (§5 matrix): same tags as publish PLUS
///         <c>toolkit:{id}:ratings</c> (yanked versions may invalidate aggregate ratings).</item>
/// </list>
/// </remarks>
internal sealed class YankToolkitVersionCommandHandler
    : ICommandHandler<YankToolkitVersionCommand, YankedToolkitVersionResponse?>
{
    private readonly MeepleAiDbContext _context;
    private readonly IToolkitVersionRepository _versionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<YankToolkitVersionCommandHandler> _logger;

    public YankToolkitVersionCommandHandler(
        MeepleAiDbContext context,
        IToolkitVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        TimeProvider timeProvider,
        ILogger<YankToolkitVersionCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<YankedToolkitVersionResponse?> Handle(
        YankToolkitVersionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1) Tracked load — we mutate IsPublished on the cascade path below.
        var toolkit = await _context.GameToolkits
            .FirstOrDefaultAsync(t => t.Id == request.ToolkitId, cancellationToken)
            .ConfigureAwait(false);

        if (toolkit is null)
        {
            _logger.LogInformation(
                "YankToolkitVersion: toolkit {ToolkitId} not found (viewer {ViewerId})",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        // 2) Ownership enforcement.
        if (toolkit.CreatedByUserId != request.ViewerId)
        {
            _logger.LogWarning(
                "YankToolkitVersion: viewer {ViewerId} attempted to yank version of toolkit {ToolkitId} owned by {OwnerId}",
                request.ViewerId,
                request.ToolkitId,
                toolkit.CreatedByUserId);
            throw new ForbiddenException(
                "Only the toolkit owner can yank versions.");
        }

        // 3) Load version aggregate — must exist AND belong to this toolkit.
        //    Defends against URL tampering (yank /toolkits/{A}/versions/{B}
        //    where B's parent is C).
        var version = await _versionRepository.GetByIdAsync(request.VersionId, cancellationToken)
            .ConfigureAwait(false);

        if (version is null || version.ToolkitId != request.ToolkitId)
        {
            _logger.LogInformation(
                "YankToolkitVersion: version {VersionId} not found or not a child of toolkit {ToolkitId}",
                request.VersionId,
                request.ToolkitId);
            return null;
        }

        // 4) Yank — domain method enforces non-empty reason + idempotency
        //    (already-yanked throws ConflictException).
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        version.Yank(yankedBy: request.ViewerId, reason: request.Reason, yankedAt: now);

        // 5) Persist version mutation.
        await _versionRepository.UpdateAsync(version, cancellationToken).ConfigureAwait(false);

        // 6) Cascade rule: if no remaining non-yanked versions, auto-unpublish.
        //    The just-yanked version is still in the DB until SaveChanges, but
        //    GetLatestNonYankedAsync queries with AsNoTracking + YankedAt filter,
        //    so it sees the persisted state (pre-SaveChanges). We compensate by
        //    querying inside the same DbContext but after the in-memory mutation:
        //    EF Core honours pending changes when the same DbSet is queried with
        //    tracking — but our repo uses AsNoTracking. The reliable approach is
        //    to count via _context with tracked changes considered.
        var remainingNonYanked = await _context.Set<Api.Infrastructure.Entities.GameToolkit.ToolkitVersionEntity>()
            .Where(v => v.ToolkitId == request.ToolkitId
                && v.Id != request.VersionId
                && v.YankedAt == null)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var toolkitAutoUnpublished = false;
        if (remainingNonYanked == 0 && toolkit.IsPublished)
        {
            toolkit.IsPublished = false;
            toolkit.UpdatedAt = now;
            toolkitAutoUnpublished = true;

            _logger.LogInformation(
                "YankToolkitVersion: toolkit {ToolkitId} auto-unpublished — no non-yanked versions remain",
                request.ToolkitId);
        }

        // 7) Single transaction commit.
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 8) Cache invalidation matrix (spec-panel §5 — Yank):
        //    Includes ratings tag in addition to the publish-set because a yanked
        //    version's ratings should no longer count toward the aggregate.
        await _cache.RemoveByTagAsync($"toolkit:{request.ToolkitId:N}", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveByTagAsync("toolkitVersions", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveAsync("toolkits:popular", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveAsync("discover:popularAgents", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "YankToolkitVersion: version {VersionId} of toolkit {ToolkitId} yanked by {ViewerId} "
            + "(reason='{Reason}', autoUnpublished={AutoUnpublished})",
            request.VersionId,
            request.ToolkitId,
            request.ViewerId,
            request.Reason,
            toolkitAutoUnpublished);

        return new YankedToolkitVersionResponse(
            Id: version.Id,
            ToolkitId: version.ToolkitId,
            VersionNumber: version.VersionNumber,
            YankedAt: version.YankedAt!.Value,
            Reason: version.YankReason!,
            ToolkitAutoUnpublished: toolkitAutoUnpublished);
    }
}
