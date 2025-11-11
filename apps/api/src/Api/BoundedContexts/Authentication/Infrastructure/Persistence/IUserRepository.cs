using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
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
}
