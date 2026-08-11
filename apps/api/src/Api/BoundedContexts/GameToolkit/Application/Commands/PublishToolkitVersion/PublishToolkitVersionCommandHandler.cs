using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.PublishToolkitVersion;

/// <summary>
/// Handler for <see cref="PublishToolkitVersionCommand"/>
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §3 + §5).
/// </summary>
/// <remarks>
/// <para>Pipeline (spec-panel §6 Gherkin):</para>
/// <list type="number">
///   <item>Load tracked toolkit row — return <c>null</c> if missing (404).</item>
///   <item>Owner check (<c>CreatedByUserId == ViewerId</c>) — throw <c>ForbiddenException</c>.</item>
///   <item>Uniqueness check — throw <c>ConflictException</c> if <c>(ToolkitId, VersionNumber)</c> exists
///         (covers yanked numbers per §1 — permanently retired).</item>
///   <item>Monotonicity check — throw <c>ConflictException</c> if not strictly greater than
///         the latest non-yanked version (spec-panel §3 strict semver compare).</item>
///   <item>Create <c>ToolkitVersion</c> via <see cref="ToolkitVersion.Publish"/> (raises domain event).</item>
///   <item>Add to repo + bump parent <c>VersionSemver</c> + flip <c>IsPublished=true</c> + <c>UpdatedAt</c>.</item>
///   <item>SaveChanges (single transaction via <see cref="IUnitOfWork"/>).</item>
///   <item>Cache invalidation (§5 matrix): <c>toolkit:{id}</c>, <c>toolkits:{id}:versions</c>,
///         <c>toolkits:popular</c>, and <c>discover:popularAgents</c> (legacy install rail).</item>
/// </list>
/// </remarks>
internal sealed class PublishToolkitVersionCommandHandler
    : ICommandHandler<PublishToolkitVersionCommand, PublishedToolkitVersionResponse?>
{
    private readonly IGameToolkitRepository _toolkitRepository;
    private readonly IToolkitVersionRepository _versionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PublishToolkitVersionCommandHandler> _logger;

    public PublishToolkitVersionCommandHandler(
        IGameToolkitRepository toolkitRepository,
        IToolkitVersionRepository versionRepository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        TimeProvider timeProvider,
        ILogger<PublishToolkitVersionCommandHandler> logger)
    {
        _toolkitRepository = toolkitRepository ?? throw new ArgumentNullException(nameof(toolkitRepository));
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PublishedToolkitVersionResponse?> Handle(
        PublishToolkitVersionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1) Load the aggregate through the repository (#3670). This used to be a TRACKED
        //    load of the EF entity via MeepleAiDbContext, because step 6 mutated the row
        //    directly to dodge MapToPersistence's VersionSemver synthesis. The aggregate now
        //    owns VersionSemver, so the normal repository path works.
        //
        //    Do NOT reintroduce a tracked load alongside UpdateAsync: GetByIdAsync is
        //    AsNoTracking, and UpdateAsync calls Set<GameToolkitEntity>().Update() with a
        //    freshly mapped instance. Two instances of the same key in the change tracker
        //    would throw ("instance with the same key value is already being tracked").
        var toolkit = await _toolkitRepository
            .GetByIdAsync(request.ToolkitId, cancellationToken)
            .ConfigureAwait(false);

        if (toolkit is null)
        {
            _logger.LogInformation(
                "PublishToolkitVersion: toolkit {ToolkitId} not found (viewer {ViewerId})",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        // 2) Ownership enforcement — never trust the client.
        if (toolkit.CreatedByUserId != request.ViewerId)
        {
            _logger.LogWarning(
                "PublishToolkitVersion: viewer {ViewerId} attempted to publish toolkit {ToolkitId} owned by {OwnerId}",
                request.ViewerId,
                request.ToolkitId,
                toolkit.CreatedByUserId);
            throw new ForbiddenException(
                "Only the toolkit owner can publish new versions.");
        }

        // 3) Uniqueness check (covers yanked numbers — permanently retired).
        var alreadyExists = await _versionRepository
            .ExistsAsync(request.ToolkitId, request.VersionNumber, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyExists)
        {
            throw new ConflictException(
                $"Version '{request.VersionNumber}' has already been used for this toolkit "
                + "(yanked numbers are permanently retired per spec-panel §1).");
        }

        // 4) Monotonicity — must be strictly greater than the latest non-yanked
        //    version. Skipped if no previous versions exist (first publish).
        var latest = await _versionRepository
            .GetLatestNonYankedAsync(request.ToolkitId, cancellationToken)
            .ConfigureAwait(false);

        if (latest is not null
            && !ToolkitVersion.IsStrictlyGreater(request.VersionNumber, latest.VersionNumber))
        {
            throw new ConflictException(
                $"Version '{request.VersionNumber}' is not strictly greater than the current "
                + $"published version '{latest.VersionNumber}'.");
        }

        // 5) Create ToolkitVersion (raises ToolkitVersionPublishedEvent).
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var version = ToolkitVersion.Publish(
            toolkitId: request.ToolkitId,
            versionNumber: request.VersionNumber,
            changelog: request.Changelog,
            publishedBy: request.ViewerId,
            publishedAt: now);

        await _versionRepository.AddAsync(version, cancellationToken).ConfigureAwait(false);

        // 6) Move the parent toolkit's marketplace pointer through the aggregate (#3670).
        //    Previously this mutated the tracked EF row directly to bypass
        //    GameToolkitRepository.UpdateAsync, whose MapToPersistence synthesised
        //    VersionSemver as "0.{Version}.0" and would have overwritten the user's input.
        //    That synthesis is gone, so the write goes through the repository like any other.
        toolkit.PublishMarketplaceVersion(request.VersionNumber, now);
        await _toolkitRepository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);

        // 7) Single transaction — IUnitOfWork dispatches domain events post-commit.
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 8) Cache invalidation matrix (spec-panel §5 — Publish):
        //    - toolkit:{id} (detail + system-prompt sub-keys)
        //    - toolkits:{id}:versions (versions list)
        //    - toolkits:popular (rating-weighted rail)
        //    - discover:popularAgents (install rail — install handler invalidates it too)
        await _cache.RemoveByTagAsync($"toolkit:{request.ToolkitId:N}", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveByTagAsync("toolkitVersions", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveAsync("toolkits:popular", cancellationToken)
            .ConfigureAwait(false);
        await _cache.RemoveAsync("discover:popularAgents", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "PublishToolkitVersion: toolkit {ToolkitId} published version {VersionNumber} by {ViewerId}",
            request.ToolkitId,
            request.VersionNumber,
            request.ViewerId);

        return new PublishedToolkitVersionResponse(
            Id: version.Id,
            ToolkitId: version.ToolkitId,
            VersionNumber: version.VersionNumber,
            Changelog: version.Changelog,
            PublishedAt: version.PublishedAt,
            PublishedBy: version.PublishedBy);
    }
}
