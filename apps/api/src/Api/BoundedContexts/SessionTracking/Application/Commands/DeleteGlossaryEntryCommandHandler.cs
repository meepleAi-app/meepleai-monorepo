using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class DeleteGlossaryEntryCommandHandler
    : IRequestHandler<DeleteGlossaryEntryCommand, Unit>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookGlossaryRepository _glossary;

    public DeleteGlossaryEntryCommandHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookGlossaryRepository glossary)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
    }

    public async Task<Unit> Handle(DeleteGlossaryEntryCommand cmd, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (campaign.OwnerUserId != cmd.CallerUserId)
        {
            throw new ForbiddenException("Forbidden");
        }

        var entry = await _glossary.GetByIdAsync(cmd.EntryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Glossary entry {cmd.EntryId} not found");

        // An entry belonging to a different campaign is treated as not-found for this
        // campaign (avoids leaking existence across campaigns; the caller only owns this one).
        if (entry.CampaignId != cmd.CampaignId)
        {
            throw new NotFoundException($"Glossary entry {cmd.EntryId} not found");
        }

        _glossary.Remove(entry);
        await _glossary.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
