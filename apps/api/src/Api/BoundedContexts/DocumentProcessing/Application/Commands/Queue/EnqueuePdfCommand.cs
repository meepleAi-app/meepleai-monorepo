using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Adds a PDF document to the processing queue.
/// Issue #4731: Queue commands.
/// </summary>
/// <remarks>
/// #4442: <c>BypassQueueCap</c> creates the job even when the queue holds
/// <c>ProcessingJob.MaxQueueSize</c> active jobs. Only for interactive admin uploads; the HTTP endpoint
/// never sets it.
/// </remarks>
internal record EnqueuePdfCommand(
    Guid PdfDocumentId,
    Guid UserId,
    int Priority = 0,
    bool BypassQueueCap = false
) : ICommand<Guid>;
