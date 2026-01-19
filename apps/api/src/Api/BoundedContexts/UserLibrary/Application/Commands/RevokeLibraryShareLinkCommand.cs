using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to revoke (delete) a library share link.
/// </summary>
internal record RevokeLibraryShareLinkCommand(
    Guid UserId,
    string ShareToken
) : ICommand;
