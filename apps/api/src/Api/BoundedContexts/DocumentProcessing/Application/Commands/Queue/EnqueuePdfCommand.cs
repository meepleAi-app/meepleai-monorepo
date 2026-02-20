using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Adds a PDF document to the processing queue.
/// Issue #4731: Queue commands.
/// </summary>
internal record EnqueuePdfCommand(
    Guid PdfDocumentId,
    Guid UserId,
    int Priority = 0
) : ICommand<Guid>;
