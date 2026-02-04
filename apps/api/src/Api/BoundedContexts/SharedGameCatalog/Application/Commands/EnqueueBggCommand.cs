using Api.Infrastructure.Entities;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to enqueue a single BGG game for import
/// Issue #3541: BGG Import Queue Service
/// </summary>
public record EnqueueBggCommand : IRequest<BggImportQueueEntity>
{
    /// <summary>
    /// BGG game ID to import
    /// </summary>
    public required int BggId { get; init; }

    /// <summary>
    /// Optional game name for UI display
    /// </summary>
    public string? GameName { get; init; }
}
