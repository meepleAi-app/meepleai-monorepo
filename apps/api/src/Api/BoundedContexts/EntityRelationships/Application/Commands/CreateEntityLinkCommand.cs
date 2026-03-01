using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Command to create a new EntityLink between two MeepleAI entities (Issue #5133).
///
/// Business Rules enforced by handler:
/// - BR-04: scope=User → IsAdminApproved = true automatically (in EntityLink.Create)
/// - BR-08: No duplicate (sourceType, sourceId, targetType, targetId, linkType)
/// Source != Target validated by EntityLink.Create (ArgumentException).
/// </summary>
internal record CreateEntityLinkCommand(
    MeepleEntityType SourceEntityType,
    Guid SourceEntityId,
    MeepleEntityType TargetEntityType,
    Guid TargetEntityId,
    EntityLinkType LinkType,
    EntityLinkScope Scope,
    Guid OwnerUserId,
    string? Metadata = null,
    bool IsBggImported = false
) : ICommand<EntityLinkDto>;
