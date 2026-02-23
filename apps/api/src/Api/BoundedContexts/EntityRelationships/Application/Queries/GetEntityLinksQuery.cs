using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Queries;

/// <summary>
/// Query to retrieve all EntityLinks for a given entity (Issue #5135).
///
/// Returns links where the entity is:
/// - the source (always)
/// - the target of a bidirectional link (IsBidirectional=true)
///
/// Supports optional filtering by Scope and LinkType.
/// Pass RequestingUserId to populate IsOwner on each returned DTO.
/// </summary>
internal record GetEntityLinksQuery(
    MeepleEntityType EntityType,
    Guid EntityId,
    Guid? RequestingUserId = null,
    EntityLinkScope? Scope = null,
    EntityLinkType? LinkType = null
) : IQuery<IReadOnlyList<EntityLinkDto>>;
