using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Sessions;

/// <summary>
/// Query to get recent gaming sessions for current user (Issue #4579)
/// Epic #4575: Gaming Hub Dashboard - Phase 1
/// </summary>
internal record GetRecentSessionsQuery : IRequest<List<SessionSummaryDto>>
{
    public int Limit { get; init; } = 3;
}

/// <summary>
/// Validator for GetRecentSessionsQuery (Issue #4579)
/// Ensures limit is between 1-20
/// </summary>
internal class GetRecentSessionsQueryValidator : AbstractValidator<GetRecentSessionsQuery>
{
    public GetRecentSessionsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .GreaterThan(0).WithMessage("Limit must be greater than 0")
            .LessThanOrEqualTo(20).WithMessage("Limit cannot exceed 20");
    }
}

/// <summary>
/// DTO for gaming session summary (Issue #4579)
/// Displays session in Recent Sessions widget
/// </summary>
internal record SessionSummaryDto(
    Guid Id,
    string GameName,
    string? GameImageUrl,
    DateTime SessionDate,
    int PlayerCount,
    TimeSpan? Duration,
    int? AverageScore,
    string? WinnerName);
