using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Issue #1903 M5.3 — materialises a <see cref="SharedGame"/> aggregate when an
/// admin approves a <see cref="CatalogSeedDraftEntity"/>, and overwrites the
/// placeholder <c>ResultingSharedGameId</c> set by
/// <see cref="ApproveCatalogSeedCommandHandler"/> with the actual aggregate Id.
/// </summary>
/// <remarks>
/// <para>Closes the M4.4 placeholder gap. The approval handler runs first (M4.4),
/// persists the draft as <c>Approved</c> with <c>ResultingSharedGameId =
/// Guid.NewGuid()</c>, then publishes <see cref="CatalogSeedApprovedEvent"/>;
/// this handler resolves that event, creates the SharedGame, and rewrites the
/// FK on the draft.</para>
/// <para><b>Mapping strategy</b>: uses <see cref="SharedGame.CreateSkeleton"/>
/// because catalog-seed provenance (BGG + Wikidata merge) frequently lacks the
/// strict fields required by <see cref="SharedGame.Create"/> — notably an
/// absolute <c>imageUrl</c>, a non-empty <c>description</c>, a year > 1900, and
/// positive player counts. The resulting aggregate lands in
/// <c>GameDataStatus.Skeleton</c> + <c>GameStatus.Draft</c>, ready for the
/// BGG-enrichment pipeline (issue #1874) or manual editing to lift it to
/// <c>Enriched</c> / <c>PendingApproval</c>.</para>
/// <para><b>Idempotency</b>: if a SharedGame already exists for the draft's
/// <c>BggId</c>, this handler logs and skips creation but still rewrites the
/// draft's <c>ResultingSharedGameId</c> to point at the existing aggregate so
/// audit consistency is preserved.</para>
/// <para><b>What stays as a follow-up</b>: enriching the skeleton in-line from
/// provenance fields like <c>yearPublished</c>, <c>minPlayers</c>, etc. would
/// require either a partial <c>EnrichFromProvenance</c> domain method or a
/// state-machine transition Skeleton → Enriching → Enriched that the seed
/// pipeline can drive. Both are deliberately out of scope for M5 — the existing
/// BGG enrichment queue (#1874) handles this once a BggId is on the aggregate.</para>
/// </remarks>
internal sealed class CatalogSeedApprovedEventHandler : INotificationHandler<CatalogSeedApprovedEvent>
{
    private readonly ICatalogSeedDraftRepository _drafts;
    private readonly ISharedGameRepository _games;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CatalogSeedApprovedEventHandler> _logger;

    public CatalogSeedApprovedEventHandler(
        ICatalogSeedDraftRepository drafts,
        ISharedGameRepository games,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CatalogSeedApprovedEventHandler> logger)
    {
        _drafts = drafts ?? throw new ArgumentNullException(nameof(drafts));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CatalogSeedApprovedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var draft = await _drafts.GetByIdAsync(notification.DraftId, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            _logger.LogWarning(
                "CatalogSeedApprovedEvent for draft {DraftId} but draft not found — race condition or test artefact, skipping",
                notification.DraftId);
            return;
        }

        if (string.IsNullOrWhiteSpace(draft.ProvenanceJson))
        {
            _logger.LogWarning(
                "CatalogSeedApprovedEvent for draft {DraftId} but ProvenanceJson is empty — cannot materialise SharedGame",
                notification.DraftId);
            return;
        }

        CatalogSeedProvenance provenance;
        try
        {
            provenance = CatalogSeedProvenance.FromJson(draft.ProvenanceJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex,
                "CatalogSeedApprovedEvent for draft {DraftId}: ProvenanceJson is malformed, skipping",
                notification.DraftId);
            return;
        }

        var title = provenance.GetValue<string>("title");
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogWarning(
                "CatalogSeedApprovedEvent for draft {DraftId}: title missing in provenance — cannot materialise SharedGame",
                notification.DraftId);
            return;
        }

        // Issue #3147/#3154 — the sparse Wikidata-primary (BGG-fallback) SCALARS to
        // carry onto a freshly-materialised skeleton. `GetValue<T?>` yields null when
        // a field is absent; SharedGame.EnrichFromProvenance applies each only when
        // present and internally consistent (see its leniency rules).
        var provYear = provenance.GetValue<int?>("yearPublished");
        var provMinPlayers = provenance.GetValue<int?>("minPlayers");
        var provMaxPlayers = provenance.GetValue<int?>("maxPlayers");
        var provPlayingTime = provenance.GetValue<int?>("playingTimeMinutes");

        // Issue #3153 — designer/publisher NAMES (Wikidata P178/P123, stored as
        // List<string>). Ingested by EnrichFromProvenance and persisted as M:N join
        // rows by SharedGameRepository.AddAsync (get-or-create by name).
        var provDesigners = provenance.GetValue<List<string>>("designers");
        var provPublishers = provenance.GetValue<List<string>>("publishers");

        // Idempotency: if a draft was approved twice, or if a SharedGame already
        // exists for the same BGG ID (collision with prior import path), reuse it.
        SharedGame? existing = null;
        if (draft.BggId.HasValue)
        {
            existing = await _games.GetByBggIdAsync(draft.BggId.Value, cancellationToken).ConfigureAwait(false);
        }

        Guid materialisedId;
        if (existing is not null)
        {
            materialisedId = existing.Id;
            _logger.LogInformation(
                "CatalogSeedApprovedEventHandler: SharedGame already exists for BggId={BggId} (Id={SharedGameId}); reusing for draft {DraftId}",
                draft.BggId, existing.Id, notification.DraftId);

            // Issue #1823 M8.5 — back-fill WikidataQid from the draft onto the
            // existing aggregate if the draft has one and the aggregate does not.
            // Idempotent: if both already share the same QID, AssignWikidataQid
            // returns silently. We deliberately do NOT overwrite a QID already
            // present on the existing aggregate (the catalog-seed pipeline is the
            // primary source, but admin-assigned QIDs win to avoid clobbering
            // human curation).
            if (!string.IsNullOrWhiteSpace(draft.WikidataQid)
                && string.IsNullOrWhiteSpace(existing.WikidataQid))
            {
                existing.AssignWikidataQid(draft.WikidataQid, notification.ApprovedByUserId);
                _games.Update(existing);
                _logger.LogInformation(
                    "CatalogSeedApprovedEventHandler: assigned WikidataQid={Qid} to existing SharedGame {SharedGameId} from draft {DraftId}",
                    draft.WikidataQid, existing.Id, notification.DraftId);
            }
        }
        else
        {
            var skeleton = SharedGame.CreateSkeleton(
                title: title,
                createdBy: notification.ApprovedByUserId,
                timeProvider: _timeProvider,
                bggId: draft.BggId);

            // Issue #1823 M8.5 — forward the WikidataQid from the catalog-seed
            // draft onto the freshly-materialised skeleton BEFORE persisting it,
            // so the WikidataCoverEnrichmentJob (M9) can pick the game up on its
            // first scan instead of waiting for a separate admin trigger.
            if (!string.IsNullOrWhiteSpace(draft.WikidataQid))
            {
                skeleton.AssignWikidataQid(draft.WikidataQid, notification.ApprovedByUserId);
            }

            // Issue #3147/#3154 — carry the Wikidata-primary scalars onto the skeleton
            // instead of dropping them (the M5 follow-up flagged in this handler's
            // remarks). Only the new-skeleton branch enriches: a BggId collision
            // (existing branch) is already covered by the BGG enrichment queue
            // (#1874), whereas a pure-Wikidata skeleton (frequently no BggId) has
            // Wikidata as its ONLY property source. Lenient — a title-only draft
            // leaves the skeleton unchanged.
            skeleton.EnrichFromProvenance(
                yearPublished: provYear,
                minPlayers: provMinPlayers,
                maxPlayers: provMaxPlayers,
                playingTimeMinutes: provPlayingTime,
                modifiedBy: notification.ApprovedByUserId,
                designers: provDesigners,
                publishers: provPublishers);

            await _games.AddAsync(skeleton, cancellationToken).ConfigureAwait(false);
            materialisedId = skeleton.Id;

            _logger.LogInformation(
                "CatalogSeedApprovedEventHandler: created SharedGame {SharedGameId} (Skeleton) for draft {DraftId} title='{Title}' BggId={BggId} WikidataQid={Qid}",
                skeleton.Id, notification.DraftId, title, draft.BggId, draft.WikidataQid);
        }

        // Overwrite the placeholder ResultingSharedGameId from M4.4 with the real Id.
        // The repo returned the draft AsTracking() so a SaveChangesAsync persists the change.
        draft.ResultingSharedGameId = materialisedId;

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
