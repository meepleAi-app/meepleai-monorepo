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

    public DeleteGamebookCampaignHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task Handle(DeleteGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (session.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Only owner can delete campaign");

        session.SoftDelete(cmd.CallerUserId);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
