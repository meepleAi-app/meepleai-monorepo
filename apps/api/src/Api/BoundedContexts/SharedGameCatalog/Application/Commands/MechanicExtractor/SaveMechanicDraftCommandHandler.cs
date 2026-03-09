using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for creating or updating a mechanic extraction draft.
/// Creates a new draft if none exists, otherwise updates the existing one.
/// </summary>
internal sealed class SaveMechanicDraftCommandHandler
    : ICommandHandler<SaveMechanicDraftCommand, MechanicDraftDto>
{
    private readonly IMechanicDraftRepository _draftRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SaveMechanicDraftCommandHandler> _logger;

    public SaveMechanicDraftCommandHandler(
        IMechanicDraftRepository draftRepository,
        IUnitOfWork unitOfWork,
        ILogger<SaveMechanicDraftCommandHandler> logger)
    {
        _draftRepository = draftRepository ?? throw new ArgumentNullException(nameof(draftRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicDraftDto> Handle(
        SaveMechanicDraftCommand request,
        CancellationToken cancellationToken)
    {
        var existingDraft = await _draftRepository.GetDraftForGameAsync(
            request.SharedGameId,
            request.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        MechanicDraft draft;

        if (existingDraft is null)
        {
            _logger.LogInformation(
                "Creating new mechanic draft for game {SharedGameId}, PDF {PdfDocumentId}",
                request.SharedGameId,
                request.PdfDocumentId);

            draft = MechanicDraft.Create(
                request.SharedGameId,
                request.PdfDocumentId,
                request.GameTitle,
                request.UserId);

            draft.UpdateAllNotes(
                request.SummaryNotes,
                request.MechanicsNotes,
                request.VictoryNotes,
                request.ResourcesNotes,
                request.PhasesNotes,
                request.QuestionsNotes);

            await _draftRepository.AddAsync(draft, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation(
                "Updating mechanic draft {DraftId} for game {SharedGameId}",
                existingDraft.Id,
                request.SharedGameId);

            draft = existingDraft;
            draft.UpdateAllNotes(
                request.SummaryNotes,
                request.MechanicsNotes,
                request.VictoryNotes,
                request.ResourcesNotes,
                request.PhasesNotes,
                request.QuestionsNotes);

            _draftRepository.Update(draft);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(draft);
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
