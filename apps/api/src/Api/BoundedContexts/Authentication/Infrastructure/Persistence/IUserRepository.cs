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
    /// Counts users with a specific role name (case-insensitive).
    /// Used for privilege escalation guards (e.g., ensure at least one SuperAdmin remains).
    /// ADM-002: Privilege escalation prevention.
    /// </summary>
    Task<int> CountByRoleAsync(string roleName, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Writes only the lockout state of the user (<c>FailedLoginAttempts</c>, <c>LockedUntil</c>) and collects its
    /// domain events, so <c>AccountLockedEvent</c> still goes out with the save.
    /// </summary>
    /// <remarks>
    /// #5036: the failed-login path runs for anyone who knows an email. <see cref="IRepository{TEntity, TKey}.UpdateAsync"/>
    /// copies every column of the aggregate read at the start of the request, so a wrong password typed while an admin
    /// suspends the user, resets the password or changes the role would write the old values back. Use this method
    /// wherever only the lockout state changed.
    /// </remarks>
    Task UpdateLockoutStateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the user row (<c>SELECT … FOR UPDATE</c>) and loads its current lockout state into <paramref name="user"/>.
    /// Must run inside <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/>: the lock holds until that commits.
    /// Returns false, and leaves <paramref name="user"/> as it was, when the row no longer exists.
    /// </summary>
    /// <remarks>
    /// #5036: concurrent wrong passwords for one account must take turns on the counter. Without the lock each request
    /// computes from its own read: increments are lost, and a slow request can write <c>LockedUntil = null</c> over a lock
    /// another one has just set.
    /// </remarks>
    Task<bool> ReloadLockoutStateForUpdateAsync(User user, CancellationToken cancellationToken = default);
}
