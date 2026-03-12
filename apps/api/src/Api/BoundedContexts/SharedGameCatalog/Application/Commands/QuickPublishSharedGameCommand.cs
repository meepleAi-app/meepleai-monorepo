using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to quick-publish a shared game, transitioning directly from Draft to Published.
/// Only available for admin users who have both submit and approve permissions.
/// Issue #250: Quick-publish endpoint for admin shared games
/// </summary>
internal record QuickPublishSharedGameCommand(
    Guid GameId,
    Guid PublishedBy
) : ICommand<Unit>;
