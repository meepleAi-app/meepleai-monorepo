using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// SSE event for PDF processing status updates.
/// Issue #4218: Real-Time Updates
/// </summary>
public sealed record PdfStatusEventDto(
    Guid DocumentId,
    PdfProcessingState State,
    int Progress,
    string? Eta,
    DateTime Timestamp,
    string? ErrorMessage = null
);
