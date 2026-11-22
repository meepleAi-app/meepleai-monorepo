using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Application.Services;

/// <summary>
/// Who is told about share requests waiting for review (#5113): the roles the review routes admit, as
/// <c>AdminOnlyPolicy</c> does (SuperAdmin, Admin). The alerts used to filter on <c>Role == "admin"</c>, so a superadmin
/// could review the requests and was never told about them, and where every administrator is a superadmin nobody was.
/// </summary>
internal static class ShareRequestAlertRecipients
{
    // As the users table stores them: the lowercase values of the Role value object.
    private static readonly string[] Roles = [Role.Admin.Value, Role.SuperAdmin.Value];

    public static IQueryable<UserEntity> WhereShareRequestAlertRecipient(this IQueryable<UserEntity> users) =>
        users.Where(u => Roles.Contains(u.Role));
}
