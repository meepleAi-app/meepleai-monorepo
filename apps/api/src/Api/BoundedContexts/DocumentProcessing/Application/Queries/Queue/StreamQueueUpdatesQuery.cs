using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Streaming query for SSE updates on the entire processing queue.
/// Issue #4732: SSE streaming for queue.
/// </summary>
internal record StreamQueueUpdatesQuery() : IStreamingQuery<QueueStreamEvent>;
