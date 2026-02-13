using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get aggregated associated data for multiple collection entries.
/// Used for bulk removal warnings.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal record GetBulkCollectionAssociatedDataQuery(
    Guid UserId,
    EntityType EntityType,
    IReadOnlyList<Guid> EntityIds
) : IQuery<BulkAssociatedDataDto>;
