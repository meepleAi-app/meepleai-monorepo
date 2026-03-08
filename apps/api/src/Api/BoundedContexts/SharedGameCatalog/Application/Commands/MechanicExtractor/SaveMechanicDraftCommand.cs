using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Command to create or update a mechanic extraction draft.
/// Supports auto-save from the editor UI.
/// </summary>
internal record SaveMechanicDraftCommand(
    Guid SharedGameId,
    Guid PdfDocumentId,
    string GameTitle,
    Guid UserId,
    string SummaryNotes,
    string MechanicsNotes,
    string VictoryNotes,
    string ResourcesNotes,
    string PhasesNotes,
    string QuestionsNotes)
    : ICommand<MechanicDraftDto>;
