using Api.BoundedContexts.EntityRelationships.Domain.Enums;

namespace Api.BoundedContexts.EntityRelationships.Domain.Exceptions;

/// <summary>
/// Thrown when an EntityLink with the same (sourceType, sourceId, targetType, targetId, linkType)
/// already exists (BR-08).
/// </summary>
public sealed class DuplicateEntityLinkException : Exception
{
    public DuplicateEntityLinkException(
        MeepleEntityType sourceType,
        Guid sourceId,
        MeepleEntityType targetType,
        Guid targetId,
        EntityLinkType linkType)
        : base($"An EntityLink from {sourceType}/{sourceId} to {targetType}/{targetId} of type '{linkType}' already exists.")
    {
    }
}
