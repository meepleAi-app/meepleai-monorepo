using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Streaming query for real-time PDF processing progress updates via SSE.
/// Returns progress events with heartbeat for connection keep-alive.
/// Issue #4209: SSE Progress Stream for Public PDFs
/// </summary>
internal sealed record StreamPdfProgressQuery(
    Guid PdfId,
    Guid UserId,
    bool IsAdmin
) : IStreamingQuery<ProcessingProgressJson>;
