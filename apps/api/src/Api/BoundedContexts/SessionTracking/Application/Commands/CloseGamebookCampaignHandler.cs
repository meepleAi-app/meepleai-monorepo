using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// SI-8 (#2639): terminally closes a gamebook campaign (Completa/Abbandona).
/// Only the owner may close (IDOR guard → 403). Closing an already-closed campaign
/// throws <see cref="ConflictException"/> (→ 409) via the aggregate's idempotency
/// guard. Returns the updated campaign DTO with its new outcome.
/// </summary>
public class CloseGamebookCampaignHandler : IRequestHandler<CloseGamebookCampaignCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;

    public CloseGamebookCampaignHandler(IGamebookCampaignSessionRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GamebookCampaignDto> Handle(CloseGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        var session = await _repo.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (session.OwnerUserId != cmd.CallerUserId)
            throw new ForbiddenException("Only owner can close campaign");

        // Aggregate enforces the already-closed idempotency guard (→ ConflictException).
        session.Close(cmd.Outcome, cmd.CallerUserId);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreateGamebookCampaignHandler.MapToDto(session, progress: null);
    }
}
