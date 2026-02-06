using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.EntityFrameworkCore;

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
    private readonly MeepleAiDbContext _meepleContext;

    public CreateProposalMigrationOnApprovalHandler(
        MeepleAiDbContext dbContext,
        IShareRequestRepository shareRequestRepo,
        IProposalMigrationRepository migrationRepo,
        ILogger<CreateProposalMigrationOnApprovalHandler> logger)
        : base(dbContext, logger)
    {
        _shareRequestRepo = shareRequestRepo;
        _migrationRepo = migrationRepo;
        _meepleContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

        // Get the PrivateGameId from the UserLibraryEntry that was used for the proposal
        // IMPORTANT: For NewGameProposal, SourceGameId should reference the UserLibraryEntry.Id
        // (not PrivateGameId or SharedGameId) that contains the proposed private game.
        // This differs from other ContributionTypes where SourceGameId references SharedGameId.
        var libraryEntry = await _meepleContext.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == shareRequest.SourceGameId, cancellationToken)
            .ConfigureAwait(false);

        if (libraryEntry == null)
        {
            Logger.LogError(
                "ShareRequest {ShareRequestId}: UserLibraryEntry {SourceGameId} not found",
                domainEvent.ShareRequestId,
                shareRequest.SourceGameId);
            return;
        }

        if (!libraryEntry.PrivateGameId.HasValue || libraryEntry.PrivateGameId.Value == Guid.Empty)
        {
            Logger.LogError(
                "ShareRequest {ShareRequestId}: UserLibraryEntry {LibraryEntryId} has no valid PrivateGameId",
                domainEvent.ShareRequestId,
                libraryEntry.Id);
            return;
        }

        // Create the ProposalMigration
        var migration = ProposalMigration.Create(
            shareRequestId: shareRequest.Id,
            privateGameId: libraryEntry.PrivateGameId.Value,
            sharedGameId: domainEvent.TargetSharedGameId.Value,
            userId: shareRequest.UserId);

        await _migrationRepo.AddAsync(migration, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Created ProposalMigration {MigrationId} for ShareRequest {ShareRequestId}: PrivateGame {PrivateGameId} → SharedGame {SharedGameId}",
            migration.Id,
            shareRequest.Id,
            libraryEntry.PrivateGameId.Value,
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
