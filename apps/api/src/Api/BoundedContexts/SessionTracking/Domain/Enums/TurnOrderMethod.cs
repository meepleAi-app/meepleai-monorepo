namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// How turn order is determined for a session.
/// </summary>
public enum TurnOrderMethod
{
    /// <summary>Order explicitly defined by host.</summary>
    Manual = 0,
    /// <summary>Order shuffled server-side with auditable seed.</summary>
    Random = 1
}
