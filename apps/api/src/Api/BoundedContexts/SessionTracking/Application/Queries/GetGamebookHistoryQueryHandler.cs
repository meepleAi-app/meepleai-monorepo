using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

internal sealed class GetGamebookHistoryQueryHandler
    : IRequestHandler<GetGamebookHistoryQuery, IReadOnlyList<TranslatedParagraphDto>>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly ITranslatedParagraphRepository _paragraphs;

    public GetGamebookHistoryQueryHandler(
        IGamebookCampaignSessionRepository campaigns,
        ITranslatedParagraphRepository paragraphs)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _paragraphs = paragraphs ?? throw new ArgumentNullException(nameof(paragraphs));
    }

    public async Task<IReadOnlyList<TranslatedParagraphDto>> Handle(
        GetGamebookHistoryQuery query, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(query.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {query.CampaignId} not found");

        if (campaign.OwnerUserId != query.CallerUserId)
            throw new ConflictException("Forbidden");

        var paragraphs = await _paragraphs.ListByCampaignAsync(query.CampaignId, cancellationToken).ConfigureAwait(false);

        return paragraphs
            .Select(p => new TranslatedParagraphDto(
                p.Id,
                p.ParagraphNumber,
                p.PageType.ToString(),
                p.SourceTextEn,
                p.TranslatedTextIt,
                p.AppliedGlossaryTerms,
                p.CreatedAt))
            .ToList()
            .AsReadOnly();
    }
}
