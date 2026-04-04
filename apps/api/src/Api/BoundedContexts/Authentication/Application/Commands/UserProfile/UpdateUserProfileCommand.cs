using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to update user profile information.
/// Allows users to update their display name and email.
/// </summary>
internal record UpdateUserProfileCommand : ICommand
{
    public Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Bio { get; init; }
}
