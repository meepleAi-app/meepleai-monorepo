using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Handler for loading a mechanic draft.
/// </summary>
internal sealed class GetMechanicDraftQueryHandler
    : IQueryHandler<GetMechanicDraftQuery, MechanicDraftDto?>
{
    private readonly IMechanicDraftRepository _draftRepository;

    public GetMechanicDraftQueryHandler(IMechanicDraftRepository draftRepository)
    {
        _draftRepository = draftRepository ?? throw new ArgumentNullException(nameof(draftRepository));
    }

    public async Task<MechanicDraftDto?> Handle(
        GetMechanicDraftQuery request,
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetDraftForGameAsync(
            request.SharedGameId,
            request.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        return draft is null ? null : MapToDto(draft);
    }

    private static MechanicDraftDto MapToDto(MechanicDraft draft)
    {
        return new MechanicDraftDto(
            draft.Id,
            draft.SharedGameId,
            draft.PdfDocumentId,
            draft.CreatedBy,
            draft.GameTitle,
            draft.SummaryNotes,
            draft.MechanicsNotes,
            draft.VictoryNotes,
            draft.ResourcesNotes,
            draft.PhasesNotes,
            draft.QuestionsNotes,
            draft.SummaryDraft,
            draft.MechanicsDraft,
            draft.VictoryDraft,
            draft.ResourcesDraft,
            draft.PhasesDraft,
            draft.QuestionsDraft,
            draft.CreatedAt,
            draft.LastModified,
            draft.Status.ToString());
    }
}
