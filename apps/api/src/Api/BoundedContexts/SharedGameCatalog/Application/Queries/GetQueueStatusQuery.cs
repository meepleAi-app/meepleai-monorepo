using Api.Infrastructure.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get current BGG import queue status
/// Issue #3541: BGG Import Queue Service
/// </summary>
public record GetQueueStatusQuery : IRequest<List<BggImportQueueEntity>>;
