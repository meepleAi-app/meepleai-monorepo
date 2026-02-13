using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to add multiple entities to user's collection in bulk.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal record BulkAddToCollectionCommand(
    Guid UserId,
    EntityType EntityType,
    IReadOnlyList<Guid> EntityIds,
    bool IsFavorite = false,
    string? Notes = null
) : ICommand<BulkOperationResult>;
