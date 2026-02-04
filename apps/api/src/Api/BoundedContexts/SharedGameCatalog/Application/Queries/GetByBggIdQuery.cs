using Api.Infrastructure.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get BGG import queue entry by BGG ID
/// Issue #3541: BGG Import Queue Service
/// </summary>
public record GetByBggIdQuery(int BggId) : IRequest<BggImportQueueEntity?>;
