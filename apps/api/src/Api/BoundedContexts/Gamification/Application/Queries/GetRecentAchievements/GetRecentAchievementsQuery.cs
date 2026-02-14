using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;

/// <summary>
/// Query to get the most recently unlocked achievements for a user.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed record GetRecentAchievementsQuery(
    Guid UserId,
    int Limit = 5
) : IQuery<List<RecentAchievementDto>>;
