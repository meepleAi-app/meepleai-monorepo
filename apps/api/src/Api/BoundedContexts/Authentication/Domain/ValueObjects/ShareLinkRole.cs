namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Defines the access level for shared chat thread links.
/// </summary>
internal enum ShareLinkRole
{
    /// <summary>
    /// Read-only access: can view thread messages and citations.
    /// </summary>
    View = 0,

    /// <summary>
    /// Comment access: can view and add new messages (rate-limited).
    /// Cannot delete or edit existing messages.
    /// </summary>
    Comment = 1
}
