using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to remove multiple entities from user's collection in bulk.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal record BulkRemoveFromCollectionCommand(
    Guid UserId,
    EntityType EntityType,
    IReadOnlyList<Guid> EntityIds
) : ICommand<BulkOperationResult>;
