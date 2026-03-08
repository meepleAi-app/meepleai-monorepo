using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for MechanicDraft entities.
/// </summary>
internal sealed class MechanicDraftRepository : IMechanicDraftRepository
{
    private readonly MeepleAiDbContext _context;

    public MechanicDraftRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MechanicDraft draft, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(draft);
        await _context.MechanicDrafts.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MechanicDraft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.MechanicDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<MechanicDraft?> GetDraftForGameAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.MechanicDrafts
            .AsNoTracking()
            .Where(d => d.SharedGameId == sharedGameId
                        && d.PdfDocumentId == pdfDocumentId
                        && d.Status != (int)MechanicDraftStatus.Activated)
            .OrderByDescending(d => d.LastModified)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<MechanicDraft>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.MechanicDrafts
            .AsNoTracking()
            .Where(d => d.SharedGameId == sharedGameId)
            .OrderByDescending(d => d.LastModified)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public void Update(MechanicDraft draft)
    {
        var entity = MapToEntity(draft);
        _context.MechanicDrafts.Update(entity);
    }

    private static MechanicDraft MapToDomain(MechanicDraftEntity entity)
    {
        return new MechanicDraft(
            entity.Id,
            entity.SharedGameId,
            entity.PdfDocumentId,
            entity.CreatedBy,
            entity.GameTitle,
            entity.SummaryNotes,
            entity.MechanicsNotes,
            entity.VictoryNotes,
            entity.ResourcesNotes,
            entity.PhasesNotes,
            entity.QuestionsNotes,
            entity.SummaryDraft,
            entity.MechanicsDraft,
            entity.VictoryDraft,
            entity.ResourcesDraft,
            entity.PhasesDraft,
            entity.QuestionsDraft,
            entity.CreatedAt,
            entity.LastModified,
            (MechanicDraftStatus)entity.Status);
    }

    private static MechanicDraftEntity MapToEntity(MechanicDraft draft)
    {
        return new MechanicDraftEntity
        {
            Id = draft.Id,
            SharedGameId = draft.SharedGameId,
            PdfDocumentId = draft.PdfDocumentId,
            CreatedBy = draft.CreatedBy,
            GameTitle = draft.GameTitle,
            SummaryNotes = draft.SummaryNotes,
            MechanicsNotes = draft.MechanicsNotes,
            VictoryNotes = draft.VictoryNotes,
            ResourcesNotes = draft.ResourcesNotes,
            PhasesNotes = draft.PhasesNotes,
            QuestionsNotes = draft.QuestionsNotes,
            SummaryDraft = draft.SummaryDraft,
            MechanicsDraft = draft.MechanicsDraft,
            VictoryDraft = draft.VictoryDraft,
            ResourcesDraft = draft.ResourcesDraft,
            PhasesDraft = draft.PhasesDraft,
            QuestionsDraft = draft.QuestionsDraft,
            CreatedAt = draft.CreatedAt,
            LastModified = draft.LastModified,
            Status = (int)draft.Status
        };
    }
}
