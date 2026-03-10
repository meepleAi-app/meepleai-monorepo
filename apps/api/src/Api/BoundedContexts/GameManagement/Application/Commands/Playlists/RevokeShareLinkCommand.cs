using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to revoke the share link for a playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record RevokeShareLinkCommand(
    Guid PlaylistId,
    Guid UserId
) : ICommand;
