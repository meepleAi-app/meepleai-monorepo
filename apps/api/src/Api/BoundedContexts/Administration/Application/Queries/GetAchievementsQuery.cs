using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

public record GetAchievementsQuery(Guid UserId) : IQuery<List<AchievementDto>>;
public record GetRecentAchievementsQuery(Guid UserId, int Count = 3) : IQuery<List<AchievementDto>>;
