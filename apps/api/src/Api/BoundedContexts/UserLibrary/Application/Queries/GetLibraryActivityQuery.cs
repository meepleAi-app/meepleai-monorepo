using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query for the activity feed displayed in <c>RecentActivityRail</c>
/// on <c>/library</c> (Issue #642 — Wave B.3 followup).
/// Aggregates <c>added</c> + <c>state-changed</c> pseudo-events from
/// <c>UserLibraryEntries</c> records as a DomainEventLog-free MVP.
/// </summary>
internal record GetLibraryActivityQuery(
    Guid UserId,
    int Limit = 20
) : IQuery<IReadOnlyList<LibraryActivityItemDto>>;
