using Api.Infrastructure.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to enqueue multiple BGG games for import in batch
/// Issue #3541: BGG Import Queue Service
/// </summary>
public record EnqueueBggBatchCommand : IRequest<List<BggImportQueueEntity>>
{
    /// <summary>
    /// Collection of BGG IDs to import
    /// </summary>
    public required List<int> BggIds { get; init; }

    /// <summary>
    /// User ID performing the import (for audit trail). Null if not authenticated.
    /// </summary>
    public Guid? RequestedByUserId { get; init; }
}
