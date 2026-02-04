using Api.Infrastructure.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all BGG import queue items (for SSE streaming)
/// Issue #3543 - Fix #3: Support completed/failed counts in SSE
/// </summary>
public record GetAllQueueItemsQuery : IRequest<List<BggImportQueueEntity>>;
