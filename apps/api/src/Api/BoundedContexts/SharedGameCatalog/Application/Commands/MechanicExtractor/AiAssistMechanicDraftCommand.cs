using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Command to request AI assistance for a specific section of a mechanic draft.
/// CRITICAL: The AI receives ONLY the human's notes, NEVER the PDF content.
/// This is the copyright firewall (Variant C workflow).
/// </summary>
internal record AiAssistMechanicDraftCommand(
    Guid DraftId,
    string Section,
    string HumanNotes,
    string GameTitle)
    : ICommand<AiAssistResultDto>;
