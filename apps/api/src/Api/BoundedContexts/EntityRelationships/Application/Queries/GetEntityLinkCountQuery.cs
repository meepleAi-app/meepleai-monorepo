using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Queries;

/// <summary>
/// Lightweight COUNT query for the EntityLink badge on MeepleCard (Issue #5136).
///
/// Counts links where the entity is:
/// - the source (always)
/// - the target of a bidirectional link (IsBidirectional=true)
///
/// Optimised: COUNT(*) with no JOINs, relies on indexed columns.
///
/// #4637: pass <c>RequestingUserId</c> to count what that user can see (the <c>User</c>-scope links of others are
/// personal and are left out). Without it, everything is counted.
/// </summary>
internal record GetEntityLinkCountQuery(
    MeepleEntityType EntityType,
    Guid EntityId,
    Guid? RequestingUserId = null
) : IQuery<int>;
