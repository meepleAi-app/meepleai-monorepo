using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to update existing user details (email, display name, role).
/// </summary>
/// <param name="UserId">The user to update.</param>
/// <param name="Email">The new email, or null to keep it.</param>
/// <param name="DisplayName">The new display name, or null to keep it.</param>
/// <param name="Role">Refused when present: role changes go through the dedicated endpoint.</param>
/// <param name="AdminRole">
/// The role of the admin acting (from the session). #4907: the target must have a role below it, because a new email
/// plus «forgot password» hands over the account. The default refuses every target.
/// </param>
[RequireTwoFactor(Reason = "Changing a user's email can hand over the account; it must be 2FA-guarded.")]
internal record UpdateUserCommand(
    string UserId,
    string? Email = null,
    string? DisplayName = null,
    string? Role = null,
    string AdminRole = "user"
) : ICommand<UserDto>;
