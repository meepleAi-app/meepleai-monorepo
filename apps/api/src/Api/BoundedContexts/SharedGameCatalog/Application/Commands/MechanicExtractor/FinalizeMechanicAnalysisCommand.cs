using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Command to finalize a mechanic draft into a RulebookAnalysis entry.
/// Converts the draft's accepted AI content into a permanent, copyright-compliant analysis.
/// </summary>
internal record FinalizeMechanicAnalysisCommand(
    Guid DraftId,
    Guid UserId)
    : ICommand<RulebookAnalysisDto>;
