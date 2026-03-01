using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Command to launch PDF processing with admin priority.
/// Sets Priority=Admin on the PDF and triggers ExtractPdfText + IndexPdf.
/// </summary>
internal record LaunchAdminPdfProcessingCommand(
    Guid GameId,
    Guid PdfDocumentId,
    Guid LaunchedByUserId
) : ICommand<LaunchProcessingResult>;

/// <summary>
/// Result of launching admin PDF processing.
/// </summary>
public record LaunchProcessingResult
{
    public required Guid PdfDocumentId { get; init; }
    public required Guid GameId { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
}
