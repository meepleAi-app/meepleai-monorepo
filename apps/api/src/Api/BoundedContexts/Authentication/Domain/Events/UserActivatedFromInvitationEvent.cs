using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a pending user activates their account from an invitation.
/// The user has set their password and transitioned from Pending to Active status.
/// </summary>
internal sealed class UserActivatedFromInvitationEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the activated user.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the email address of the activated user.
    /// </summary>
    public string Email { get; }

    /// <summary>
    /// Gets the user's role.
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Gets the user's tier.
    /// </summary>
    public string Tier { get; }

    /// <summary>
    /// Gets the ID of the admin who originally invited this user.
    /// </summary>
    public Guid InvitedByUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserActivatedFromInvitationEvent"/> class.
    /// </summary>
    public UserActivatedFromInvitationEvent(
        Guid userId,
        string email,
        string role,
        string tier,
        Guid invitedByUserId)
    {
        UserId = userId;
        Email = email;
        Role = role;
        Tier = tier;
        InvitedByUserId = invitedByUserId;
    }
}
