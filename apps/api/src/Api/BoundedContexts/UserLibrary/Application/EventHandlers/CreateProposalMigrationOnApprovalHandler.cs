using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Event handler that creates a ProposalMigration when a NewGameProposal is approved.
/// This allows the user to choose whether to link their library entry to the new SharedGame.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
internal sealed class CreateProposalMigrationOnApprovalHandler : DomainEventHandlerBase<ShareRequestApprovedEvent>
{
    private readonly IShareRequestRepository _shareRequestRepo;
    private readonly IProposalMigrationRepository _migrationRepo;

    public CreateProposalMigrationOnApprovalHandler(
        MeepleAiDbContext dbContext,
        IShareRequestRepository shareRequestRepo,
        IProposalMigrationRepository migrationRepo,
        ILogger<CreateProposalMigrationOnApprovalHandler> logger)
        : base(dbContext, logger)
    {
        _shareRequestRepo = shareRequestRepo;
        _migrationRepo = migrationRepo;
    }

    protected override async Task HandleEventAsync(
        ShareRequestApprovedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Get the approved ShareRequest
        var shareRequest = await _shareRequestRepo.GetByIdAsync(
            domainEvent.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            Logger.LogWarning(
                "ShareRequest {ShareRequestId} not found for ProposalMigration creation",
                domainEvent.ShareRequestId);
            return;
        }

        // Only create migration for NewGameProposal type
        if (shareRequest.ContributionType != ContributionType.NewGameProposal)
        {
            Logger.LogDebug(
                "ShareRequest {ShareRequestId} is not a NewGameProposal, skipping ProposalMigration creation",
                domainEvent.ShareRequestId);
            return;
        }

        // TargetSharedGameId must be set (the newly created SharedGame)
        if (!domainEvent.TargetSharedGameId.HasValue)
        {
            Logger.LogError(
                "ShareRequest {ShareRequestId} approved as NewGameProposal but no TargetSharedGameId provided",
                domainEvent.ShareRequestId);
            return;
        }

        // #5078: the proposed game is the proposal's private game (ShareRequest.CreateGameProposal), as
        // ApproveGameProposalCommandHandler reads it (#5076). This used to read a UserLibraryEntry at SourceGameId,
        // which is Guid.Empty for a proposal: no migration would ever have been recorded.
        if (shareRequest.SourcePrivateGameId is not { } privateGameId || privateGameId == Guid.Empty)
        {
            Logger.LogError(
                "ShareRequest {ShareRequestId} approved as NewGameProposal but has no proposed private game",
                domainEvent.ShareRequestId);
            return;
        }

        // Create the ProposalMigration
        // Issue #1938 / CF-2: propagate EventId so the resulting row carries the source
        // event id and the UNIQUE partial index blocks duplicate inserts on retry/replay.
        var migration = ProposalMigration.Create(
            shareRequestId: shareRequest.Id,
            privateGameId: privateGameId,
            sharedGameId: domainEvent.TargetSharedGameId.Value,
            userId: shareRequest.UserId,
            sourceEventId: domainEvent.EventId);

        // CF-2 / #1938 deferred follow-up: AddAndCommitAsync wraps AddAsync + an
        // inline SaveChangesAsync with a 23505 catch. Returns false when an outbox
        // replay (#1535 at-least-once delivery) or concurrent pod already wrote the
        // row keyed by this EventId — log and skip silently so the legitimate
        // replay doesn't surface as an alarm.
        if (!await _migrationRepo.AddAndCommitAsync(migration, cancellationToken).ConfigureAwait(false))
        {
            Logger.LogInformation(
                "Skipping duplicate ProposalMigration for ShareRequest {ShareRequestId}: SourceEventId={SourceEventId} (already recorded)",
                shareRequest.Id, domainEvent.EventId);
            return;
        }

        Logger.LogInformation(
            "Created ProposalMigration {MigrationId} for ShareRequest {ShareRequestId}: PrivateGame {PrivateGameId} → SharedGame {SharedGameId}",
            migration.Id,
            shareRequest.Id,
            privateGameId,
            domainEvent.TargetSharedGameId.Value);
    }

    protected override Guid? GetUserId(ShareRequestApprovedEvent domainEvent)
        => domainEvent.AdminId;

    protected override Dictionary<string, object?>? GetAuditMetadata(ShareRequestApprovedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["ShareRequestId"] = domainEvent.ShareRequestId,
            ["TargetSharedGameId"] = domainEvent.TargetSharedGameId
        };
}
