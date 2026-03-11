using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

/// <summary>
/// Repository interface for InvitationToken aggregate.
/// </summary>
internal interface IInvitationTokenRepository : IRepository<InvitationToken, Guid>
{
    /// <summary>
    /// Finds an invitation token by its hash.
    /// </summary>
    Task<InvitationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a pending invitation for a specific email address.
    /// </summary>
    Task<InvitationToken?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of invitations, optionally filtered by status.
    /// </summary>
    Task<IReadOnlyList<InvitationToken>> GetByStatusAsync(
        InvitationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts invitations with a specific status.
    /// </summary>
    Task<int> CountByStatusAsync(InvitationStatus status, CancellationToken cancellationToken = default);
}
