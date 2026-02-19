using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Paginated queue listing with optional filters.
/// Issue #4731: Queue queries.
/// </summary>
internal record GetProcessingQueueQuery(
    string? StatusFilter = null,
    string? SearchText = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedQueueResponse>;
