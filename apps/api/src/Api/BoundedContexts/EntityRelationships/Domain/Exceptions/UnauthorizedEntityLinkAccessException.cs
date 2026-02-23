namespace Api.BoundedContexts.EntityRelationships.Domain.Exceptions;

/// <summary>
/// Thrown when a user attempts to delete an EntityLink they do not own
/// or when a non-admin user tries to delete a BGG-imported link (A5 / Issue #5134).
/// </summary>
public sealed class UnauthorizedEntityLinkAccessException : Exception
{
    public UnauthorizedEntityLinkAccessException(Guid entityLinkId, Guid requestingUserId, string reason)
        : base($"User '{requestingUserId}' is not authorized to delete EntityLink '{entityLinkId}': {reason}")
    {
    }
}
