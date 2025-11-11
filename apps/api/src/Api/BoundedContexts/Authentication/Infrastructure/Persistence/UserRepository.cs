using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of User repository.
/// Maps between domain User entity and UserEntity persistence model.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userEntity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var userEntity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Value, cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email.Value, cancellationToken);
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userEntities = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return userEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = MapToPersistence(entity);
        await _dbContext.Users.AddAsync(userEntity, cancellationToken);
    }

    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = MapToPersistence(entity);
        _dbContext.Users.Update(userEntity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = await _dbContext.Users.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (userEntity != null)
        {
            _dbContext.Users.Remove(userEntity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == id, cancellationToken);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static User MapToDomain(Api.Infrastructure.Entities.UserEntity entity)
    {
        var email = new Email(entity.Email);
        var passwordHash = PasswordHash.FromStored(entity.PasswordHash);
        var role = Role.Parse(entity.Role);

        var user = new User(
            id: entity.Id,
            email: email,
            displayName: entity.DisplayName,
            passwordHash: passwordHash,
            role: role
        );

        // Use reflection to set 2FA properties (since they're private set)
        if (entity.IsTwoFactorEnabled && !string.IsNullOrEmpty(entity.TotpSecretEncrypted))
        {
            var enableMethod = typeof(User).GetMethod("EnableTwoFactor");
            enableMethod?.Invoke(user, new object[] { entity.TotpSecretEncrypted });
        }

        return user;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.UserEntity MapToPersistence(User domainEntity)
    {
        return new Api.Infrastructure.Entities.UserEntity
        {
            Id = domainEntity.Id,
            Email = domainEntity.Email.Value,
            DisplayName = domainEntity.DisplayName,
            PasswordHash = domainEntity.PasswordHash.Value,
            Role = domainEntity.Role.Value,
            CreatedAt = domainEntity.CreatedAt,
            TotpSecretEncrypted = domainEntity.TotpSecretEncrypted,
            IsTwoFactorEnabled = domainEntity.IsTwoFactorEnabled,
            TwoFactorEnabledAt = domainEntity.TwoFactorEnabledAt
        };
    }
}
