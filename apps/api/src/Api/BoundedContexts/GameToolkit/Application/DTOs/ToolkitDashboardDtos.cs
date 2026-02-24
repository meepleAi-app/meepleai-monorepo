#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
using Api.BoundedContexts.GameToolkit.Domain.Enums;

namespace Api.BoundedContexts.GameToolkit.Application.DTOs;

/// <summary>
/// Response DTO for the user's active Toolkit (default or per-user override).
/// Issue #5147 — Epic B4.
/// </summary>
internal record ToolkitDashboardDto(
    Guid Id,
    Guid GameId,
    Guid? OwnerUserId,
    bool IsDefault,
    string? DisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ToolkitWidgetDto> Widgets
);

/// <summary>DTO for a single widget slot.</summary>
internal record ToolkitWidgetDto(
    Guid Id,
    WidgetType Type,
    bool IsEnabled,
    int DisplayOrder,
    string Config
);

/// <summary>Request body for overriding (cloning) the default Toolkit.</summary>
internal record OverrideToolkitRequest(string? DisplayName);

/// <summary>Request body for updating a single widget's state/config.</summary>
internal record UpdateWidgetRequest(
    bool? IsEnabled,
    string? ConfigJson
);
