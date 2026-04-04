using Api.SharedKernel.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.BulkUploadPdfs;

/// <summary>
/// Command to bulk upload multiple PDF documents for a shared game.
/// Issue #117: Bulk PDF Upload for shared game content management.
/// </summary>
internal record BulkUploadPdfsCommand(
    Guid SharedGameId,
    Guid UserId,
    IReadOnlyList<IFormFile> Files
) : ICommand<BulkUploadPdfsResult>;

/// <summary>
/// Result of a bulk PDF upload operation.
/// </summary>
internal record BulkUploadPdfsResult(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<BulkUploadItemResult> Items
);

/// <summary>
/// Individual upload result for each file in the bulk operation.
/// </summary>
internal record BulkUploadItemResult(
    string FileName,
    bool Success,
    Guid? DocumentId,
    string? Error
);
