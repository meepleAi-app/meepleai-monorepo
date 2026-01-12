using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to delete a shared game (soft delete).
/// Only admins can directly delete games. Editors must request approval via RequestDeleteSharedGameCommand.
/// </summary>
internal record DeleteSharedGameCommand(
    Guid GameId,
    Guid DeletedBy
) : ICommand<Unit>;
