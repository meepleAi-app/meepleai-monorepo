using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Soft-deletes a gamebook campaign (sets IsDeleted=true + DeletedAt).
/// EF query filter on the entity hides soft-deleted rows from subsequent reads;
/// associated photos and glossary entries remain in DB for audit but become
/// unreachable through normal queries (campaign id no longer resolves).
/// Only the owner can delete.
/// </summary>
public class DeleteGamebookCampaignHandler : IRequestHandler<DeleteGamebookCampaignCommand>
{
    private readonly IGamebookCampaignSessionRepository _repo;
    private readonly ISessionBookProgressRepository _progress;
    private readonly IMediator _mediator;

    public DeleteGamebookCampaignHandler(
        IGamebookCampaignSessionRepository repo,
        ISessionBookProgressRepository progress,
        IMediator mediator)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task Handle(DeleteGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (session.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Only owner can delete campaign");

        // Issue #1394: SessionBookProgress rows have no FK cascade to the campaign,
        // so we must explicitly purge orphans before the unit-of-work flush. The
        // shared DbContext means SaveChangesAsync persists both the soft-delete
        // and the orphan removal atomically.
        await _progress.DeleteByCampaignAsync(session.Id, cancellationToken).ConfigureAwait(false);

        session.SoftDelete(cmd.CallerUserId);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #1292 (AC-6.2): notify gamebook index cache so the deleted
        // campaign disappears from next GET /api/v1/gamebooks within 500ms.
        await _mediator.Publish(
            new GamebookCampaignDeletedDomainEvent(session.Id, session.GameRef.Id, session.OwnerUserId),
            cancellationToken).ConfigureAwait(false);
    }
}
