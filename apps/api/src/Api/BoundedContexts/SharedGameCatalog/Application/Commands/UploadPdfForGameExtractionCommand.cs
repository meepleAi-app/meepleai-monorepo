using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to upload a PDF file for temporary storage during game metadata extraction wizard.
/// Stores file in temporary location without quota checks or background processing.
/// File will be cleaned up automatically after 24 hours or when wizard completes.
/// </summary>
/// <param name="File">PDF file to upload (IFormFile)</param>
/// <param name="UserId">ID of the user uploading the file (for audit trail)</param>
public record UploadPdfForGameExtractionCommand(
    IFormFile File,
    Guid UserId
) : ICommand<TempPdfUploadResult>;
