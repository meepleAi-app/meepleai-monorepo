using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to publish a shared game, making it visible to all users.
/// Game must be in Draft status to be published.
/// </summary>
internal record PublishSharedGameCommand(
    Guid GameId,
    Guid PublishedBy
) : ICommand<Unit>;
