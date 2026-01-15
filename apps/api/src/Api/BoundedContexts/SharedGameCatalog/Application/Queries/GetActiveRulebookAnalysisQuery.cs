using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get the active rulebook analysis for a game and PDF document.
/// Returns null if no active analysis exists.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
/// <param name="PdfDocumentId">The ID of the PDF document</param>
internal record GetActiveRulebookAnalysisQuery(
    Guid SharedGameId,
    Guid PdfDocumentId
) : IQuery<RulebookAnalysisDto?>;
