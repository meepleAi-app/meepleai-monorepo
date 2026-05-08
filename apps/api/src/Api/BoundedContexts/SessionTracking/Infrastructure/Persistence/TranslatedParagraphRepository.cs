using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal sealed class TranslatedParagraphRepository : ITranslatedParagraphRepository
{
    private readonly MeepleAiDbContext _db;

    public TranslatedParagraphRepository(MeepleAiDbContext db) => _db = db;

    public async Task<IReadOnlyList<TranslatedParagraph>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        return await _db.TranslatedParagraphs
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.ParagraphNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(TranslatedParagraph paragraph, CancellationToken cancellationToken = default)
        => _db.TranslatedParagraphs.AddAsync(paragraph, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
