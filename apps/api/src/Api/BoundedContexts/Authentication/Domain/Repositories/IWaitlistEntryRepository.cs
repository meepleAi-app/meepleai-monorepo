using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

/// <summary>
/// Repository for the public Alpha-program waitlist aggregate.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal interface IWaitlistEntryRepository : IRepository<WaitlistEntry, Guid>
{
    /// <summary>
    /// Returns the waitlist entry matching the (already-normalized lowercase) email, or null.
    /// Used to detect duplicate enrollments (HTTP 409 path).
    /// </summary>
    Task<WaitlistEntry?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the largest <c>Position</c> currently stored, or null when the table is empty.
    /// Callers compute <c>newPosition = (max ?? 0) + 1</c>.
    /// </summary>
    Task<int?> GetMaxPositionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a Postgres transaction-scoped advisory lock that serializes Position assignment
    /// across concurrent <c>POST /waitlist</c> requests. Must be called inside an active transaction
    /// (the lock auto-releases on COMMIT/ROLLBACK).
    /// </summary>
    /// <remarks>
    /// On non-Postgres providers (in-memory test doubles) this is a no-op so unit tests
    /// using mocked repositories remain unaffected.
    /// </remarks>
    Task AcquirePositionLockAsync(CancellationToken cancellationToken = default);
}
