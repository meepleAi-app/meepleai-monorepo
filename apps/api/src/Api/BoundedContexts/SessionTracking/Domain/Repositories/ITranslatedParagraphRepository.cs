using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

public interface ITranslatedParagraphRepository
{
    Task<IReadOnlyList<TranslatedParagraph>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task AddAsync(TranslatedParagraph paragraph, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
