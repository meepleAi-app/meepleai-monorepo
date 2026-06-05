using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Dashboard;

/// <summary>
/// Asse C (#1898) WP1 T1 DEC-2: returns recent activity from "friends"
/// (User-linked players who have shared a GameNight with the current user in the
/// last 90 days). Powers the dashboard "Cosa fanno i tuoi" section.
/// </summary>
/// <param name="UserId">Current user id (extracted from session/JWT claim by the endpoint).</param>
/// <param name="Limit">Max activities to return (1..50, default 10).</param>
internal sealed record GetFriendsActivityQuery(Guid UserId, int Limit = 10)
    : IQuery<IReadOnlyList<FriendActivityDto>>;

/// <summary>
/// Validates <see cref="GetFriendsActivityQuery"/> parameters.
/// </summary>
internal sealed class GetFriendsActivityQueryValidator : AbstractValidator<GetFriendsActivityQuery>
{
    public GetFriendsActivityQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId is required.");

        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .LessThanOrEqualTo(50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}
