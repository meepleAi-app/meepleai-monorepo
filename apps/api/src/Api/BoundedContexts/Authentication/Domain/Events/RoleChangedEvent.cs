using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user's role is changed.
/// </summary>
internal sealed class RoleChangedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user whose role was changed.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the old role name.
    /// </summary>
    public string OldRole { get; }

    /// <summary>
    /// Gets the new role name.
    /// </summary>
    public string NewRole { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleChangedEvent"/> class.
    /// </summary>
    public RoleChangedEvent(Guid userId, Role oldRole, Role newRole)
    {
        UserId = userId;
        OldRole = oldRole.Value;
        NewRole = newRole.Value;
    }
}
