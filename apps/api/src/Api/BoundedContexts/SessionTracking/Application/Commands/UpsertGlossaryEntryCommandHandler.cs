using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class UpsertGlossaryEntryCommandHandler
    : IRequestHandler<UpsertGlossaryEntryCommand, GamebookGlossaryEntryDto>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookGlossaryRepository _glossary;

    public UpsertGlossaryEntryCommandHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookGlossaryRepository glossary)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
    }

    public async Task<GamebookGlossaryEntryDto> Handle(
        UpsertGlossaryEntryCommand cmd, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (campaign.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Forbidden");

        var existing = await _glossary.GetByIdAsync(cmd.EntryId, cancellationToken).ConfigureAwait(false);

        // Issue #1312: cross-entry termIt collision check. We allow the same
        // entry to set its own termIt to its current value (no-op) and only
        // flag a collision when a DIFFERENT entry already uses the target.
        var colliding = await _glossary
            .GetByTermItAsync(cmd.CampaignId, cmd.TermIt, cancellationToken)
            .ConfigureAwait(false);
        if (colliding is not null && colliding.Id != cmd.EntryId)
        {
            throw new GlossaryTermCollisionException(colliding.Id, colliding.TermEn);
        }

        GamebookGlossaryEntry entry;
        if (existing is null)
        {
            entry = GamebookGlossaryEntry.Create(
                cmd.CampaignId, cmd.TermEn, cmd.TermIt, GlossarySource.Manual, cmd.CallerUserId);
            await _glossary.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (existing.CampaignId != cmd.CampaignId)
                throw new ConflictException("Entry does not belong to this campaign");

            existing.UpdateTermIt(cmd.TermIt, cmd.CallerUserId);
            entry = existing;
        }

        await _glossary.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new GamebookGlossaryEntryDto(entry.Id, entry.TermEn, entry.TermIt, entry.Source.ToString(), entry.UpdatedAt);
    }
}
