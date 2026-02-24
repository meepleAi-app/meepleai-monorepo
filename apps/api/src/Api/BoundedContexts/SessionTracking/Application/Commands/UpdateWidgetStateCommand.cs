using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Updates the runtime state of a single widget within a session.
/// Auto-creates the ToolkitSessionState record if it doesn't exist yet.
/// Issue #5148 — Epic B5.
/// </summary>
internal record UpdateWidgetStateCommand(
    Guid SessionId,
    Guid ToolkitId,
    string WidgetType,
    string StateJson,
    Guid UserId) : ICommand<ToolkitSessionStateDto>;
