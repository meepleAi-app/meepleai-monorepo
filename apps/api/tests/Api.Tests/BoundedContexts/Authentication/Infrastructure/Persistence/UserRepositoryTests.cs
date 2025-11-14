using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for UserRepository using Testcontainers with real PostgreSQL.
/// Tests actual EF Core queries and domain-persistence mapping.
/// </summary>
[Collection("Integration")]
public class UserRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private UserRepository? _repository;

    public async ValueTask InitializeAsync()
    {
        // Start PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        // Create DbContext with container connection string
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new MeepleAiDbContext(options);

        // Apply migrations
        await _dbContext.Database.MigrateAsync();

        _repository = new UserRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task Test01_GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var user = CreateTestUser("test@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("test@example.com", result.Email.Value);
        Assert.Equal(Role.User, result.Role);
    }

    [Fact]
    public async Task Test02_GetByIdAsync_NonExistingUser_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByEmailAsync Tests

    [Fact]
    public async Task Test03_GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var user = CreateTestUser("user@test.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        var email = new Email("user@test.com");

        // Act
        var result = await _repository.GetByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("user@test.com", result.Email.Value);
    }

    [Fact]
    public async Task Test04_GetByEmailAsync_CaseInsensitive_ReturnsUser()
    {
        // Arrange
        var user = CreateTestUser("CaseSensitive@Example.COM", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        var emailLowercase = new Email("casesensitive@example.com");

        // Act
        var result = await _repository.GetByEmailAsync(emailLowercase);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task Test05_GetByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        // Arrange
        var email = new Email("nonexistent@example.com");

        // Act
        var result = await _repository!.GetByEmailAsync(email);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ExistsByEmailAsync Tests

    [Fact]
    public async Task Test06_ExistsByEmailAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser("exists@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        var email = new Email("exists@example.com");

        // Act
        var exists = await _repository.ExistsByEmailAsync(email);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task Test07_ExistsByEmailAsync_NonExistingUser_ReturnsFalse()
    {
        // Arrange
        var email = new Email("notexists@example.com");

        // Act
        var exists = await _repository!.ExistsByEmailAsync(email);

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region HasAnyUsersAsync Tests

    [Fact]
    public async Task Test08_HasAnyUsersAsync_EmptyDatabase_ReturnsFalse()
    {
        // Act
        var hasUsers = await _repository!.HasAnyUsersAsync();

        // Assert
        Assert.False(hasUsers);
    }

    [Fact]
    public async Task Test09_HasAnyUsersAsync_PopulatedDatabase_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser("firstuser@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        var hasUsers = await _repository.HasAnyUsersAsync();

        // Assert
        Assert.True(hasUsers);
    }

    #endregion

    #region CountAdminsAsync Tests

    [Fact]
    public async Task Test10_CountAdminsAsync_NoAdmins_ReturnsZero()
    {
        // Arrange
        var user1 = CreateTestUser("user1@example.com", Role.User);
        var user2 = CreateTestUser("user2@example.com", Role.Editor);
        await _repository!.AddAsync(user1);
        await _repository.AddAsync(user2);
        await _dbContext!.SaveChangesAsync();

        // Act
        var adminCount = await _repository.CountAdminsAsync();

        // Assert
        Assert.Equal(0, adminCount);
    }

    [Fact]
    public async Task Test11_CountAdminsAsync_MultipleAdmins_ReturnsCorrectCount()
    {
        // Arrange
        var admin1 = CreateTestUser("admin1@example.com", Role.Admin);
        var admin2 = CreateTestUser("admin2@example.com", Role.Admin);
        var user = CreateTestUser("user@example.com", Role.User);
        var editor = CreateTestUser("editor@example.com", Role.Editor);

        await _repository!.AddAsync(admin1);
        await _repository.AddAsync(admin2);
        await _repository.AddAsync(user);
        await _repository.AddAsync(editor);
        await _dbContext!.SaveChangesAsync();

        // Act
        var adminCount = await _repository.CountAdminsAsync();

        // Assert
        Assert.Equal(2, adminCount);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task Test12_GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var users = await _repository!.GetAllAsync();

        // Assert
        Assert.Empty(users);
    }

    [Fact]
    public async Task Test13_GetAllAsync_MultipleUsers_ReturnsAll()
    {
        // Arrange
        var user1 = CreateTestUser("user1@example.com", Role.User);
        var user2 = CreateTestUser("user2@example.com", Role.Admin);
        var user3 = CreateTestUser("user3@example.com", Role.Editor);

        await _repository!.AddAsync(user1);
        await _repository.AddAsync(user2);
        await _repository.AddAsync(user3);
        await _dbContext!.SaveChangesAsync();

        // Act
        var users = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, users.Count);
        Assert.Contains(users, u => u.Email.Value == "user1@example.com");
        Assert.Contains(users, u => u.Email.Value == "user2@example.com");
        Assert.Contains(users, u => u.Email.Value == "user3@example.com");
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task Test14_AddAsync_NewUser_PersistsSuccessfully()
    {
        // Arrange
        var user = CreateTestUser("newuser@example.com", Role.User);

        // Act
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted);
        Assert.Equal("newuser@example.com", persisted.Email);
        Assert.Equal("user", persisted.Role);
    }

    [Fact]
    public async Task Test15_AddAsync_UserWith2FA_PersistsCorrectly()
    {
        // Arrange
        var user = CreateTestUser("2fa@example.com", Role.User);
        user.EnableTwoFactor("encrypted_totp_secret_123");

        // Act
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted.IsTwoFactorEnabled);
        Assert.Equal("encrypted_totp_secret_123", persisted.TotpSecretEncrypted);
        Assert.NotNull(persisted.TwoFactorEnabledAt);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Test16_UpdateAsync_ModifiedUser_PersistsChanges()
    {
        // Arrange
        var user = CreateTestUser("update@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Modify user
        user.UpdateDisplayName("Updated Name");
        user.UpdateRole(Role.Admin);

        // Act
        await _repository.UpdateAsync(user);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("admin", updated.Role);
    }

    [Fact]
    public async Task Test17_UpdateAsync_Enable2FA_UpdatesCorrectly()
    {
        // Arrange
        var user = CreateTestUser("enable2fa@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Enable 2FA
        user.EnableTwoFactor("new_encrypted_secret");

        // Act
        await _repository.UpdateAsync(user);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsTwoFactorEnabled);
        Assert.Equal("new_encrypted_secret", updated.TotpSecretEncrypted);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task Test18_DeleteAsync_ExistingUser_RemovesFromDatabase()
    {
        // Arrange
        var user = CreateTestUser("delete@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(user);
        await _dbContext.SaveChangesAsync();

        // Assert
        var deleted = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Test19_DeleteAsync_NonExistingUser_DoesNotThrow()
    {
        // Arrange
        var user = CreateTestUser("nonexistent@example.com", Role.User);

        // Act & Assert - Should not throw
        await _repository!.DeleteAsync(user);
        await _dbContext!.SaveChangesAsync();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task Test20_ExistsAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser("exists@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsAsync(user.Id);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task Test21_ExistsAsync_NonExistingUser_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var exists = await _repository!.ExistsAsync(nonExistentId);

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region Mapping Tests

    [Fact]
    public async Task Test22_Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        var user = CreateTestUser("mapping@example.com", Role.Admin);
        user.UpdateDisplayName("Mapping Test");

        // Act
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted);
        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal(user.Email.Value, persisted.Email);
        Assert.Equal(user.DisplayName, persisted.DisplayName);
        Assert.Equal(user.PasswordHash.Value, persisted.PasswordHash);
        Assert.Equal(user.Role.Value, persisted.Role);
        Assert.Equal(user.CreatedAt, persisted.CreatedAt);
    }

    [Fact]
    public async Task Test23_Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        var user = CreateTestUser("roundtrip@example.com", Role.Editor);
        user.UpdateDisplayName("Round Trip Test");
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.Id);
        Assert.Equal(user.Email.Value, retrieved.Email.Value);
        Assert.Equal(user.DisplayName, retrieved.DisplayName);
        Assert.Equal(user.Role.Value, retrieved.Role.Value);
        Assert.Equal(user.IsTwoFactorEnabled, retrieved.IsTwoFactorEnabled);
    }

    [Fact]
    public async Task Test24_Mapping_2FAFields_PreservedCorrectly()
    {
        // Arrange
        var user = CreateTestUser("2famapping@example.com", Role.User);
        user.EnableTwoFactor("secret_123");
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsTwoFactorEnabled);
        Assert.Equal("secret_123", retrieved.TotpSecretEncrypted);
        Assert.NotNull(retrieved.TwoFactorEnabledAt);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Test25_ConcurrentReads_NoConflicts()
    {
        // Arrange
        var user = CreateTestUser("concurrent@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act - Multiple concurrent reads
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _repository.GetByIdAsync(user.Id))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.Id);
        });
    }

    [Fact]
    public async Task Test26_ConcurrentEmailLookups_NoConflicts()
    {
        // Arrange
        var user = CreateTestUser("concurrentemail@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        var email = new Email("concurrentemail@example.com");

        // Act - Multiple concurrent email lookups
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _repository.GetByEmailAsync(email))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.Id);
        });
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task Test27_Transaction_Rollback_NoDataPersisted()
    {
        // Arrange
        var user = CreateTestUser("transaction@example.com", Role.User);

        // Act
        await _repository!.AddAsync(user);
        // DO NOT call SaveChangesAsync - simulates rollback

        // Assert
        var notPersisted = await _dbContext!.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(notPersisted);
    }

    [Fact]
    public async Task Test28_MultipleOperations_SingleTransaction_AllOrNothing()
    {
        // Arrange
        var user1 = CreateTestUser("trans1@example.com", Role.User);
        var user2 = CreateTestUser("trans2@example.com", Role.Admin);

        // Act
        await _repository!.AddAsync(user1);
        await _repository.AddAsync(user2);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var count = await _dbContext.Users.CountAsync();
        Assert.Equal(2, count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Test29_NullableFields_HandledCorrectly()
    {
        // Arrange
        var user = CreateTestUser("nullable@example.com", Role.User);
        // DisplayName is nullable in some cases

        // Act
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(user.Id);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task Test30_SpecialCharacters_EmailHandling()
    {
        // Arrange
        var user = CreateTestUser("user+tag@sub.domain.com", Role.User);

        // Act
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        var email = new Email("user+tag@sub.domain.com");
        var retrieved = await _repository.GetByEmailAsync(email);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("user+tag@sub.domain.com", retrieved.Email.Value);
    }

    [Fact]
    public async Task Test31_LargeDataset_PerformanceTest()
    {
        // Arrange - Add 100 users
        var users = Enumerable.Range(1, 100)
            .Select(i => CreateTestUser($"user{i}@example.com", Role.User))
            .ToList();

        foreach (var user in users)
        {
            await _repository!.AddAsync(user);
        }
        await _dbContext!.SaveChangesAsync();

        // Act
        var allUsers = await _repository!.GetAllAsync();

        // Assert
        Assert.Equal(100, allUsers.Count);
    }

    [Fact]
    public async Task Test32_AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        var user = CreateTestUser("notracking@example.com", Role.User);
        await _repository!.AddAsync(user);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Modify retrieved object
        retrieved!.UpdateDisplayName("Modified");

        // SaveChanges without explicit Update call
        await _dbContext!.SaveChangesAsync();

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotEqual("Modified", reloaded!.DisplayName);
    }

    #endregion

    #region Helper Methods

    private static User CreateTestUser(string email, Role role)
    {
        var emailVo = new Email(email);
        var passwordHash = PasswordHash.Create("Password123!");
        var displayName = $"User {email.Split('@')[0]}";

        return new User(
            id: Guid.NewGuid(),
            email: emailVo,
            displayName: displayName,
            passwordHash: passwordHash,
            role: role
        );
    }

    #endregion
}
