using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Command to remove a user-custom cover image for a game in their library (L3).
/// Issue #1824 (umbrella #1821, cover stack L3).
/// </summary>
internal record RemoveCustomCoverCommand(
    Guid UserId,
    Guid GameId
) : ICommand;
