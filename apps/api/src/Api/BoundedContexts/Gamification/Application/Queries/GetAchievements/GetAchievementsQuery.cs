using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;

/// <summary>
/// Query to get all achievements with user's unlock status and progress.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed record GetAchievementsQuery(
    Guid UserId
) : IQuery<List<AchievementDto>>;
