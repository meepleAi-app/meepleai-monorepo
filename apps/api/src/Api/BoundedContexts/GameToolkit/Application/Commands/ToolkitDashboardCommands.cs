#pragma warning disable MA0048 // File name must match type name - Contains related commands
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Clones the default Toolkit into a user-specific override (BR-02).
/// If the user already has an override, returns it unchanged.
/// Issue #5147 — Epic B4.
/// </summary>
internal record OverrideToolkitCommand(
    Guid GameId,
    Guid UserId,
    string? DisplayName) : ICommand<ToolkitDashboardDto>;

/// <summary>
/// Enables or disables a widget, and/or updates its config JSON.
/// Automatically creates a user override if the active toolkit is the default (BR-02).
/// Issue #5147 — Epic B4.
/// </summary>
internal record UpdateWidgetCommand(
    Guid GameId,
    Guid UserId,
    WidgetType WidgetType,
    bool? IsEnabled,
    string? ConfigJson) : ICommand<ToolkitDashboardDto>;
