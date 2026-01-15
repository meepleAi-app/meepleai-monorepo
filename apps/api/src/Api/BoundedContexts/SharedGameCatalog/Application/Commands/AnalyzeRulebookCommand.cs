using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to analyze a rulebook PDF and extract structured game information.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
/// <param name="PdfDocumentId">The ID of the PDF document to analyze.</param>
/// <param name="SharedGameId">The ID of the shared game this analysis belongs to.</param>
/// <param name="UserId">The ID of the user triggering the analysis.</param>
internal record AnalyzeRulebookCommand(
    Guid PdfDocumentId,
    Guid SharedGameId,
    Guid UserId)
    : ICommand<AnalyzeRulebookResultDto>;
