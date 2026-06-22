using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

internal sealed class GetGamebookGlossaryQueryHandler
    : IRequestHandler<GetGamebookGlossaryQuery, IReadOnlyList<GamebookGlossaryEntryDto>>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookGlossaryRepository _glossary;

    public GetGamebookGlossaryQueryHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookGlossaryRepository glossary)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
    }

    public async Task<IReadOnlyList<GamebookGlossaryEntryDto>> Handle(
        GetGamebookGlossaryQuery query, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(query.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {query.CampaignId} not found");

        if (campaign.OwnerUserId != query.CallerUserId)
            throw new ForbiddenException("Forbidden");

        var entries = await _glossary.ListByCampaignAsync(query.CampaignId, cancellationToken).ConfigureAwait(false);

        return entries
            .Select(e => new GamebookGlossaryEntryDto(e.Id, e.TermEn, e.TermIt, e.Source.ToString(), e.UpdatedAt))
            .ToList()
            .AsReadOnly();
    }
}
