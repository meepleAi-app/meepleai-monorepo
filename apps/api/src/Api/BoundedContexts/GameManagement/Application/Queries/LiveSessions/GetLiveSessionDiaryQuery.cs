using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve all diary entries for a live session in chronological order.
/// <para>
/// <see cref="UserId"/> is the authenticated caller's id used for participant authz —
/// the handler throws <see cref="Api.Middleware.Exceptions.ForbiddenException"/> (→ HTTP 403)
/// when the caller is neither the creator nor an active player of the session.
/// Mirrors the authz pattern in <c>GetLiveSessionStreamContextQueryHandler</c> (SP2 T4).
/// </para>
/// Issue #2570 SP3 T4 / T5 authz fix.
/// </summary>
internal record GetLiveSessionDiaryQuery(Guid SessionId, Guid UserId) : IQuery<IReadOnlyList<DiaryEntryDto>>;
