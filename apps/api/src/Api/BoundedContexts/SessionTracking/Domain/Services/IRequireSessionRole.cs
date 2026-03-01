using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Marker interface for commands that require session role validation.
/// Commands implementing this interface will be validated by the
/// ValidatePlayerRoleBehavior pipeline behavior before handler execution.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public interface IRequireSessionRole
{
    /// <summary>Session to validate role against.</summary>
    Guid SessionId { get; }

    /// <summary>User ID of the requester.</summary>
    Guid RequesterId { get; }

    /// <summary>Minimum role required to execute this command.</summary>
    ParticipantRole MinimumRole { get; }
}
