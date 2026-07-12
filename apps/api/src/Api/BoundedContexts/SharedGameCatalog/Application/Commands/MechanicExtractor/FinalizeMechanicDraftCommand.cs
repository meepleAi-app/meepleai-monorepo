using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Command to finalize a Variant-C <c>MechanicDraft</c> into a copyright-compliant
/// <c>RulebookAnalysis</c>. Terminal step of the manual/legacy Variant-C draft flow
/// (Save → AiAssist → Accept → Finalize); it operates on a <c>MechanicDraft</c> and does NOT
/// touch the AI-first <c>MechanicAnalysis</c> aggregate or create a <c>mechanic_card</c> (#2783).
/// </summary>
internal record FinalizeMechanicDraftCommand(
    Guid DraftId,
    Guid UserId)
    : ICommand<RulebookAnalysisDto>;
