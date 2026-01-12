using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to archive a shared game, removing it from public view.
/// Archived games can no longer be published or modified.
/// </summary>
internal record ArchiveSharedGameCommand(
    Guid GameId,
    Guid ArchivedBy
) : ICommand<Unit>;
