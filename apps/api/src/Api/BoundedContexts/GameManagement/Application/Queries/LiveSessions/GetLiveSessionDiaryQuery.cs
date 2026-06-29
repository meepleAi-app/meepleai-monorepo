using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve all diary entries for a live session in chronological order.
/// Issue #2570 SP3 T4.
/// </summary>
internal record GetLiveSessionDiaryQuery(Guid SessionId) : IQuery<IReadOnlyList<DiaryEntryDto>>;
