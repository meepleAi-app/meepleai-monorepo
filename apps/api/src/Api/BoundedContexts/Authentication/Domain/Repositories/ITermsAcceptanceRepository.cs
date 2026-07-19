using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

/// <summary>
/// Repository for the append-only TermsAcceptance record (#2954 F1).
/// </summary>
public interface ITermsAcceptanceRepository
{
    /// <summary>
    /// Adds a new acceptance row to the change tracker. Does NOT SaveChanges — the
    /// caller commits via its Unit of Work, so registration can batch it into one
    /// transaction with the new user (mirrors SessionRepository).
    /// </summary>
    Task AddAsync(TermsAcceptance acceptance, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's most recent acceptance (by AcceptedAt), or null if none.</summary>
    Task<TermsAcceptance?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
