using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to request deletion of a shared game.
/// Editors create a delete request that must be approved by an admin.
/// </summary>
internal record RequestDeleteSharedGameCommand(
    Guid GameId,
    Guid RequestedBy,
    string Reason
) : ICommand<Guid>;
