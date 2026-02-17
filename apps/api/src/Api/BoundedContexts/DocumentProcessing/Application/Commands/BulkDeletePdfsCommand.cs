using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to bulk delete multiple PDF documents.
/// PDF Storage Management Hub: Phase 4.
/// </summary>
internal record BulkDeletePdfsCommand(List<Guid> PdfIds) : ICommand<BulkDeleteResult>;

internal record BulkDeleteResult(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    List<BulkDeleteItemResult> Items
);

internal record BulkDeleteItemResult(
    Guid PdfId,
    bool Success,
    string? Error
);
