using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Playlists;

/// <summary>
/// Command to generate a shareable link for a playlist.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal record GenerateShareLinkCommand(
    Guid PlaylistId,
    Guid UserId
) : ICommand<ShareLinkResponse>;
