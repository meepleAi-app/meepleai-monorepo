namespace Api.BoundedContexts.EntityRelationships.Domain.Exceptions;

/// <summary>
/// Thrown when an EntityLink with the same (sourceType, sourceId, targetType, targetId, linkType)
/// already exists (BR-08).
/// </summary>
public sealed class DuplicateEntityLinkException : Exception
{
    public DuplicateEntityLinkException(string sourceType, Guid sourceId, string targetType, Guid targetId, string linkType)
        : base($"An EntityLink from {sourceType}/{sourceId} to {targetType}/{targetId} of type '{linkType}' already exists.")
    {
    }
}
