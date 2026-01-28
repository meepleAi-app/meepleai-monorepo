using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update PDF upload limits configuration.
/// Creates configurations if they don't exist, updates if they do.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal record UpdatePdfUploadLimitsCommand(
    long MaxFileSizeBytes,
    int MaxPagesPerDocument,
    int MaxDocumentsPerGame,
    string[] AllowedMimeTypes,
    Guid UpdatedByUserId
) : ICommand<PdfUploadLimitsDto>;
