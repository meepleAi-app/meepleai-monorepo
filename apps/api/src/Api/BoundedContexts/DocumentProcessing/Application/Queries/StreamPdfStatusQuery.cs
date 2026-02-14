using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Streaming query for real-time PDF processing status updates.
/// Returns SSE events on state changes and periodic progress updates.
/// Issue #4218: Real-Time Updates (SSE + Polling)
/// </summary>
internal sealed record StreamPdfStatusQuery(
    Guid PdfId,
    Guid UserId,
    bool IsAdmin
) : IStreamingQuery<PdfStatusEventDto>;
