using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// The command behind <c>POST /api/v1/library/entity-links</c>: a user links two entities by hand (#4325).
/// </summary>
/// <remarks>
/// Not <see cref="CreateEntityLinkCommand"/>, which stays what it was: the flows that link on their own (the upload of a
/// rulebook, the reuse of a PDF by deduplication) send that one to the mediator directly, with an owner and entities
/// that the flow itself has just created or found. A check of "does the caller own this" belongs to the request of
/// a user, and this command is where it is made, before it hands the link over.
/// </remarks>
internal record CreateUserEntityLinkCommand(
    MeepleEntityType SourceEntityType,
    Guid SourceEntityId,
    MeepleEntityType TargetEntityType,
    Guid TargetEntityId,
    EntityLinkType LinkType,
    Guid OwnerUserId,
    string? Metadata = null
) : ICommand<EntityLinkDto>;
