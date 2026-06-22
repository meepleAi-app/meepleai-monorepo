using Api.BoundedContexts.Administration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for staging allowlist entries (DevOps Wave 1).
/// Exposes a hot-path read method <see cref="GetAllowedEmailsAsync"/> for the
/// <c>IStagingAccessGuard</c> cache, plus standard CRUD for admin endpoints.
/// </summary>
internal interface IStagingAllowlistRepository : IRepository<StagingAllowlistEntry, Guid>
{
    /// <summary>
    /// Returns the active (non-soft-deleted) entry matching <paramref name="normalizedEmail"/>
    /// (case-insensitive — caller must pass the email already normalized via
    /// <see cref="StagingAllowlistEntry.NormalizeEmail"/>).
    /// </summary>
    Task<StagingAllowlistEntry?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when an active entry exists for the given email.
    /// Used by the add command to detect duplicates before insert.
    /// </summary>
    Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of normalized emails currently allowed.
    /// Read by <c>StagingAccessGuard</c> for cached membership checks; should be a single
    /// indexed query (<c>SELECT email FROM staging_allowlist WHERE is_deleted = false</c>).
    /// </summary>
    Task<IReadOnlySet<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken = default);
}
