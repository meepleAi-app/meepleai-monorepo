using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when an admin provisions a new user account in Pending state.
/// The user has not yet set their password or activated their account.
/// </summary>
internal sealed class UserProvisionedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the provisioned user.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the email address of the provisioned user.
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets the display name of the provisioned user.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the assigned role.
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Gets the assigned tier.
    /// </summary>
    public string Tier { get; }

    /// <summary>
    /// Gets the ID of the admin who invited this user.
    /// </summary>
    public Guid InvitedByUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProvisionedEvent"/> class.
    /// </summary>
    public UserProvisionedEvent(
        Guid userId,
        string email,
        string displayName,
        string role,
        string tier,
        Guid invitedByUserId)
    {
        UserId = userId;
        Email = email;
        DisplayName = displayName;
        Role = role;
        Tier = tier;
        InvitedByUserId = invitedByUserId;
    }
}
