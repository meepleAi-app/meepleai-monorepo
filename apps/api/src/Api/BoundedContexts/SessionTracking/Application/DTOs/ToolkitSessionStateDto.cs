#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

/// <summary>
/// DTO for the full toolkit session state.
/// Issue #5148 — Epic B5.
/// </summary>
internal record ToolkitSessionStateDto(
    Guid Id,
    Guid SessionId,
    Guid ToolkitId,
    IReadOnlyDictionary<string, string> WidgetStates,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Request body to update a single widget's runtime state.
/// Issue #5148 — Epic B5.
/// </summary>
internal record UpdateWidgetStateRequest(string StateJson);
