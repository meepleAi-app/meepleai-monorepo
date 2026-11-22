using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Who is told about the operational alerts of the AI providers (#5119): circuit breaker, OpenRouter budget and rate,
/// deprecated models, the Redis rate limiter. They filtered on <c>Role == "admin"</c>, so a superadmin, who manages the
/// providers, was never told, and where every administrator is a superadmin nobody was. The roles are the ones
/// <c>AdminOnlyPolicy</c> admits (SuperAdmin, Admin), as for share requests (#5113); kept apart from that helper
/// because the two lists answer different questions and may part.
/// </summary>
internal static class OperationalAlertRecipients
{
    // As the users table stores them: the lowercase values of the Role value object.
    private static readonly string[] Roles = [Role.Admin.Value, Role.SuperAdmin.Value];

    public static IQueryable<UserEntity> WhereOperationalAlertRecipient(this IQueryable<UserEntity> users) =>
        users.Where(u => Roles.Contains(u.Role));
}
