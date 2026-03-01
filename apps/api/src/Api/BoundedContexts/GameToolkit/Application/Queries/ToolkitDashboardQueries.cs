using Api.BoundedContexts.GameToolkit.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Queries;

/// <summary>
/// Returns the active Toolkit for a given game and user.
/// Falls back to the shared default if no user-specific override exists.
/// Issue #5147 — Epic B4.
/// </summary>
internal record GetActiveToolkitQuery(Guid GameId, Guid UserId) : IRequest<ToolkitDashboardDto?>;
