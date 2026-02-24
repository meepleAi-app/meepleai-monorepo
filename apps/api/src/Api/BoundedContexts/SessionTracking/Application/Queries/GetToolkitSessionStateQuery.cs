using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Returns the current Toolkit widget states for a session.
/// Issue #5148 — Epic B5.
/// </summary>
internal record GetToolkitSessionStateQuery(Guid SessionId, Guid ToolkitId, Guid UserId) : IRequest<ToolkitSessionStateDto?>;
