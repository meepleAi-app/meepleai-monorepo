using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Returns snapshot + photos + scores + recap for resume experience.
/// Issue #122 — Enhanced Save/Resume.
/// </summary>
internal sealed record GetSessionResumeContextQuery(Guid SessionId)
    : IQuery<SessionResumeContextDto>;
