using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;

namespace Api.BoundedContexts.EntityRelationships.Application.DTOs;

/// <summary>
/// DTO representing an EntityLink returned from commands and queries.
/// IsOwner is populated by query handlers when a RequestingUserId is available.
/// KbCardStatus is populated when TargetEntityType = KbCard (Issue #5188).
/// </summary>
public sealed record EntityLinkDto(
    Guid Id,
    MeepleEntityType SourceEntityType,
    Guid SourceEntityId,
    MeepleEntityType TargetEntityType,
    Guid TargetEntityId,
    EntityLinkType LinkType,
    bool IsBidirectional,
    EntityLinkScope Scope,
    Guid OwnerUserId,
    string? Metadata,
    bool IsAdminApproved,
    bool IsBggImported,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// True when the requesting user is the owner of this link.
    /// Defaults to false; set by query handlers via <c>with { IsOwner = ... }</c>.
    /// </summary>
    public bool IsOwner { get; init; }

    /// <summary>
    /// Status of the KB card document linked by this EntityLink.
    /// Only populated when TargetEntityType = KbCard (Issue #5188).
    /// </summary>
    public KbCardStatusDto? KbCardStatus { get; init; }

    /// <summary>Maps an EntityLink aggregate to its DTO representation.</summary>
    public static EntityLinkDto FromEntity(EntityLink link) => new(
        link.Id,
        link.SourceEntityType,
        link.SourceEntityId,
        link.TargetEntityType,
        link.TargetEntityId,
        link.LinkType,
        link.IsBidirectional,
        link.Scope,
        link.OwnerUserId,
        link.Metadata,
        link.IsAdminApproved,
        link.IsBggImported,
        link.CreatedAt,
        link.UpdatedAt);
}
