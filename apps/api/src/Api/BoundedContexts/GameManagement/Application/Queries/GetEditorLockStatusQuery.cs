using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Issue #2055: Query to get the current lock status for a RuleSpec.
/// </summary>
public record GetEditorLockStatusQuery(
    Guid GameId,
    Guid CurrentUserId
) : IQuery<EditorLockDto>;
