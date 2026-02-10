using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve user's activity timeline (Issue #3973).
/// </summary>
internal record GetActivityTimelineQuery(
    Guid UserId,
    int Limit = 10
) : IQuery<ActivityTimelineResponseDto>;
