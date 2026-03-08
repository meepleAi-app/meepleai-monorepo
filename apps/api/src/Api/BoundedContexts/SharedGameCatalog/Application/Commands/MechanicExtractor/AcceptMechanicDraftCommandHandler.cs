using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for accepting an AI-generated draft for a specific section.
/// </summary>
internal sealed class AcceptMechanicDraftCommandHandler
    : ICommandHandler<AcceptMechanicDraftCommand, MechanicDraftDto>
{
    private readonly IMechanicDraftRepository _draftRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcceptMechanicDraftCommandHandler> _logger;

    public AcceptMechanicDraftCommandHandler(
        IMechanicDraftRepository draftRepository,
        IUnitOfWork unitOfWork,
        ILogger<AcceptMechanicDraftCommandHandler> logger)
    {
        _draftRepository = draftRepository ?? throw new ArgumentNullException(nameof(draftRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicDraftDto> Handle(
        AcceptMechanicDraftCommand request,
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetByIdAsync(request.DraftId, cancellationToken)
            .ConfigureAwait(false);

        if (draft is null)
        {
            throw new NotFoundException($"Mechanic draft {request.DraftId} not found");
        }

        _logger.LogInformation(
            "Accepting AI draft for draft {DraftId}, section {Section}",
            request.DraftId,
            request.Section);

        draft.AcceptDraft(request.Section, request.AcceptedDraft);
        _draftRepository.Update(draft);

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
