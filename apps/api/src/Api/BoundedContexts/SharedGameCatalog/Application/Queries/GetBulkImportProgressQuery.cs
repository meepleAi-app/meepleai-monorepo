using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get bulk import progress for SSE streaming.
/// Issue #4353: Backend - Bulk Import SSE Progress Endpoint
/// </summary>
public record GetBulkImportProgressQuery : IRequest<BulkImportProgressDto>;
