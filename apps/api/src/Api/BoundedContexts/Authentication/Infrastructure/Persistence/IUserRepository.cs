using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Repository interface for User aggregate root.
/// Provides data access operations specific to users.
/// </summary>
public interface IUserRepository : IRepository<User, Guid>
{
    /// <summary>
    /// Finds a user by their email address.
    /// </summary>
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user with the given email exists.
    /// </summary>
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any users exist in the system.
    /// Used for first-user-is-admin logic.
    /// </summary>
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of admin users in the system.
    /// Used to prevent deletion of the last admin.
    /// </summary>
    Task<int> CountAdminsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all backup codes for a user (used for 2FA).
    /// </summary>
    Task<IReadOnlyList<Api.Infrastructure.Entities.UserBackupCodeEntity>> GetBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches users by display name or email.
    /// Returns domain entities for autocomplete scenarios (e.g., @mentions).
    /// </summary>
    Task<IReadOnlyList<User>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all admin users (Admin and SuperAdmin roles) for notification purposes.
    /// Issue #4159: Approval workflow extension - Admin notifications
    /// </summary>
    Task<IReadOnlyList<User>> GetAdminUsersAsync(CancellationToken cancellationToken = default);
}
