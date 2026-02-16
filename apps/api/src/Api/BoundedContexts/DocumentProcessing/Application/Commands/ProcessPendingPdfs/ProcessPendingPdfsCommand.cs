using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;

/// <summary>
/// Command to process all pending PDFs in batch.
/// Triggers IndexPdfCommand for each PDF with status "pending" or "processing".
/// Admin-only operation for fixing stuck PDF processing.
/// </summary>
internal record ProcessPendingPdfsCommand : ICommand<ProcessPendingPdfsResult>;

/// <summary>
/// Result of batch PDF processing operation
/// </summary>
/// <param name="TotalPending">Total PDFs found in pending/processing status</param>
/// <param name="Triggered">Number of PDFs successfully triggered for processing</param>
/// <param name="Failed">Number of PDFs that failed to trigger</param>
/// <param name="PdfIds">List of PDF IDs that were triggered</param>
internal record ProcessPendingPdfsResult(
    int TotalPending,
    int Triggered,
    int Failed,
    List<string> PdfIds
);
