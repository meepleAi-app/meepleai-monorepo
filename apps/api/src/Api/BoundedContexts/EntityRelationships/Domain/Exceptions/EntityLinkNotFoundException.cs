namespace Api.BoundedContexts.EntityRelationships.Domain.Exceptions;

/// <summary>
/// Thrown when an EntityLink cannot be found by the given identifier.
/// </summary>
public sealed class EntityLinkNotFoundException : Exception
{
    public EntityLinkNotFoundException(Guid id)
        : base($"EntityLink with id '{id}' was not found.")
    {
    }
}
