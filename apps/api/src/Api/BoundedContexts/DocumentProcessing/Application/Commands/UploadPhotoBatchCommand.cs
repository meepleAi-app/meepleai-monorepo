using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to upload a batch of photos for OCR processing.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.3.
/// </summary>
/// <param name="UserId">The user submitting the batch</param>
/// <param name="GameId">The game associated with this rulebook batch</param>
/// <param name="SourceLanguage">BCP-47 language code of the rulebook text (e.g. "it", "en")</param>
/// <param name="Photos">One or more photos to process, each encoded as base64</param>
internal record UploadPhotoBatchCommand(
    Guid UserId,
    Guid GameId,
    string SourceLanguage,
    PhotoUploadDto[] Photos
) : ICommand<UploadPhotoBatchResult>;

/// <summary>
/// A single photo item within a batch upload.
/// </summary>
/// <param name="Filename">Original filename (e.g. "page_01.jpg")</param>
/// <param name="Base64Content">Base64-encoded raw image bytes</param>
internal record PhotoUploadDto(string Filename, string Base64Content);

/// <summary>
/// Result returned after a batch upload is accepted.
/// </summary>
/// <param name="BatchId">ID of the created <see cref="Domain.Entities.PhotoBatchUpload"/> aggregate</param>
/// <param name="AcceptedCount">Number of photos accepted into the batch</param>
internal record UploadPhotoBatchResult(Guid BatchId, int AcceptedCount);
