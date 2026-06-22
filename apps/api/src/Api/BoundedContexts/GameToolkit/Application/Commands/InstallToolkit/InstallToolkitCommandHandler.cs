using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.InstallToolkit;

/// <summary>
/// Handler for <see cref="InstallToolkitCommand"/> — idempotent install
/// action that returns the post-install state.
/// Wave 3 Phase 2, PR #732 §5.3.5 / Issue #805.
/// </summary>
/// <remarks>
/// Idempotency contract (PR #732 §5.3.5 Nygard note): calling install
/// repeatedly for the same (ViewerId, ToolkitId) pair MUST return 200 with
/// the current state — never 409. This avoids client retry loops on
/// network blips and matches the "install" mental model of "ensure it's
/// in my library" rather than "create a new install record".
///
/// Cache invalidation side-effect: removes <c>discover:popularAgents</c>
/// (Phase 1 cache key) so the discover rail picks up the new install on
/// the next read. Also removes the toolkit detail cache for the viewer so
/// <c>ViewerContext.HasInstalled</c> reflects the new state immediately.
///
/// Schema reality v1 carryover (Gate B): no <c>ToolkitInstallation</c>
/// entity exists. This handler does NOT persist anything in v1 — it just
/// validates the toolkit is installable (exists + not yanked) and returns
/// the stub response. Wire shape is stable so the FE mutation hook can
/// optimistically update without a fetch shape change once Phase 4 ships
/// the real installation upsert.
/// </remarks>
internal sealed class InstallToolkitCommandHandler
    : IRequestHandler<InstallToolkitCommand, InstallToolkitResponse?>
{
    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<InstallToolkitCommandHandler> _logger;

    public InstallToolkitCommandHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<InstallToolkitCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstallToolkitResponse?> Handle(
        InstallToolkitCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Existence + visibility check. PR #732 §5.3.5: install requires the
        // toolkit to be reachable (not 404 / not yanked). v1 yank flag does
        // not exist yet (Gate B carryover) — placeholder structure preserved.
        var entity = await _context.GameToolkits
            .AsNoTracking()
            .Where(t => t.Id == request.ToolkitId)
            .Select(t => new
            {
                t.Id,
                t.IsPublished,
                t.TemplateStatus,
                t.CreatedByUserId,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogInformation(
                "InstallToolkit: toolkit {ToolkitId} not found (viewer {ViewerId})",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        var isOwner = entity.CreatedByUserId == request.ViewerId;
        var isPublished = entity.IsPublished
            && (TemplateStatus)entity.TemplateStatus == TemplateStatus.Approved;

        // Non-owners cannot install drafts (PR #732 §5.2 boundary). Owners
        // installing their own toolkit is allowed (matches "test the install
        // flow on my own toolkit" use case).
        if (!isOwner && !isPublished)
        {
            _logger.LogInformation(
                "InstallToolkit: toolkit {ToolkitId} not installable for viewer {ViewerId} (not published, not owner)",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        // ── v1 stub install (no ToolkitInstallation entity) ────────────────
        // Idempotency contract preserved: every call returns the same shape
        // regardless of whether it's the first or Nth install for the viewer.
        const int installCount = 0;
        var response = new InstallToolkitResponse(
            InstallCount: installCount,
            HasInstalled: true);

        // Side-effect: invalidate the discover popularity cache so the rail
        // reflects the install on the next read. Tag-based removal scoops up
        // any per-viewer or per-limit variants under the same tag namespace.
        await _cache.RemoveAsync("discover:popularAgents", cancellationToken)
            .ConfigureAwait(false);
        // Per-viewer toolkit detail cache must invalidate so `HasInstalled`
        // reflects the new state on the next /toolkits/{id} call.
        await _cache.RemoveByTagAsync($"toolkit:{request.ToolkitId:N}", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "InstallToolkit: viewer {ViewerId} installed toolkit {ToolkitId} (idempotent, installCount={InstallCount})",
            request.ViewerId,
            request.ToolkitId,
            installCount);

        return response;
    }
}
