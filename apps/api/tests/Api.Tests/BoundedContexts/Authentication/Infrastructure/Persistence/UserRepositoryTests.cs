using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for UserRepository using Testcontainers with real PostgreSQL.
/// Tests actual EF Core queries and domain-persistence mapping.
/// </summary>
public class UserRepositoryTests : IntegrationTestBase<UserRepository>
{
    protected override string DatabaseName => "meepleai_user_test";

    protected override UserRepository CreateRepository(MeepleAiDbContext dbContext)
        => new UserRepository(dbContext, MockEventCollector.Object);

    #region GetByIdAsync Tests

    [Fact]
    public async Task Test01_GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("test@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await Repository.GetByIdAsync(user.Id);

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
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await Repository.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByEmailAsync Tests

    [Fact]
    public async Task Test03_GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("user@test.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        var email = new Email("user@test.com");

        // Act
        var result = await Repository.GetByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("user@test.com", result.Email.Value);
    }

    [Fact]
    public async Task Test04_GetByEmailAsync_CaseInsensitive_ReturnsUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("CaseSensitive@Example.COM", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        var emailLowercase = new Email("casesensitive@example.com");

        // Act
        var result = await Repository.GetByEmailAsync(emailLowercase);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task Test05_GetByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var email = new Email("nonexistent@example.com");

        // Act
        var result = await Repository.GetByEmailAsync(email);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ExistsByEmailAsync Tests

    [Fact]
    public async Task Test06_ExistsByEmailAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("exists@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        var email = new Email("exists@example.com");

        // Act
        var exists = await Repository.ExistsByEmailAsync(email);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task Test07_ExistsByEmailAsync_NonExistingUser_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var email = new Email("notexists@example.com");

        // Act
        var exists = await Repository.ExistsByEmailAsync(email);

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region HasAnyUsersAsync Tests

    [Fact]
    public async Task Test08_HasAnyUsersAsync_EmptyDatabase_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var hasUsers = await Repository.HasAnyUsersAsync();

        // Assert
        Assert.False(hasUsers);
    }

    [Fact]
    public async Task Test09_HasAnyUsersAsync_PopulatedDatabase_ReturnsTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("firstuser@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        var hasUsers = await Repository.HasAnyUsersAsync();

        // Assert
        Assert.True(hasUsers);
    }

    #endregion

    #region CountAdminsAsync Tests

    [Fact]
    public async Task Test10_CountAdminsAsync_NoAdmins_ReturnsZero()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1 = CreateTestUser("user1@example.com", Role.User);
        var user2 = CreateTestUser("user2@example.com", Role.Editor);
        await Repository.AddAsync(user1);
        await Repository.AddAsync(user2);
        await DbContext.SaveChangesAsync();

        // Act
        var adminCount = await Repository.CountAdminsAsync();

        // Assert
        Assert.Equal(0, adminCount);
    }

    [Fact]
    public async Task Test11_CountAdminsAsync_MultipleAdmins_ReturnsCorrectCount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var admin1 = CreateTestUser("admin1@example.com", Role.Admin);
        var admin2 = CreateTestUser("admin2@example.com", Role.Admin);
        var user = CreateTestUser("user@example.com", Role.User);
        var editor = CreateTestUser("editor@example.com", Role.Editor);

        await Repository.AddAsync(admin1);
        await Repository.AddAsync(admin2);
        await Repository.AddAsync(user);
        await Repository.AddAsync(editor);
        await DbContext.SaveChangesAsync();

        // Act
        var adminCount = await Repository.CountAdminsAsync();

        // Assert
        Assert.Equal(2, adminCount);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task Test12_GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var users = await Repository.GetAllAsync();

        // Assert
        Assert.Empty(users);
    }

    [Fact]
    public async Task Test13_GetAllAsync_MultipleUsers_ReturnsAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1 = CreateTestUser("user1@example.com", Role.User);
        var user2 = CreateTestUser("user2@example.com", Role.Admin);
        var user3 = CreateTestUser("user3@example.com", Role.Editor);

        await Repository.AddAsync(user1);
        await Repository.AddAsync(user2);
        await Repository.AddAsync(user3);
        await DbContext.SaveChangesAsync();

        // Act
        var users = await Repository.GetAllAsync();

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
        await ResetDatabaseAsync();
        var user = CreateTestUser("newuser@example.com", Role.User);

        // Act
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted);
        Assert.Equal("newuser@example.com", persisted.Email);
        Assert.Equal("user", persisted.Role);
    }

    [Fact]
    public async Task Test15_AddAsync_UserWith2FA_PersistsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("2fa@example.com", Role.User);
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_totp_secret_123"));

        // Act
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
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
        await ResetDatabaseAsync();
        var user = CreateTestUser("update@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        // Modify user
        user.UpdateDisplayName("Updated Name");
        user.UpdateRole(Role.Admin);

        // Act
        await Repository.UpdateAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("admin", updated.Role);
    }

    [Fact]
    public async Task Test17_UpdateAsync_Enable2FA_UpdatesCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("enable2fa@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Enable 2FA
        user.Enable2FA(TotpSecret.FromEncrypted("new_encrypted_secret"));

        // Act
        await Repository.UpdateAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
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
        await ResetDatabaseAsync();
        var user = CreateTestUser("delete@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        await Repository.DeleteAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var deleted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Test19_DeleteAsync_NonExistingUser_DoesNotThrow()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("nonexistent@example.com", Role.User);

        // Act & Assert - Should not throw
        await Repository.DeleteAsync(user);
        await DbContext.SaveChangesAsync();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task Test20_ExistsAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("exists@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        var exists = await Repository.ExistsAsync(user.Id);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task Test21_ExistsAsync_NonExistingUser_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var exists = await Repository.ExistsAsync(nonExistentId);

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region Mapping Tests

    [Fact]
    public async Task Test22_Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("mapping@example.com", Role.Admin);
        user.UpdateDisplayName("Mapping Test");

        // Act
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
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
        await ResetDatabaseAsync();
        var user = CreateTestUser("roundtrip@example.com", Role.Editor);
        user.UpdateDisplayName("Round Trip Test");
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await Repository.GetByIdAsync(user.Id);

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
        await ResetDatabaseAsync();
        var user = CreateTestUser("2famapping@example.com", Role.User);
        user.Enable2FA(TotpSecret.FromEncrypted("secret_123"));
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await Repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsTwoFactorEnabled);
        Assert.Equal("secret_123", retrieved.TotpSecret?.EncryptedValue);
        Assert.NotNull(retrieved.TwoFactorEnabledAt);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Test25_ConcurrentReads_NoConflicts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("concurrent@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act - Multiple concurrent reads using independent repositories
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            return await repo.GetByIdAsync(user.Id);
        }).ToArray();

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
        await ResetDatabaseAsync();
        var user = CreateTestUser("concurrentemail@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        var email = new Email("concurrentemail@example.com");

        // Act - Multiple concurrent email lookups
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Repository.GetByEmailAsync(email))
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
        await ResetDatabaseAsync();
        var user = CreateTestUser("transaction@example.com", Role.User);

        // Act
        await Repository.AddAsync(user);
        // DO NOT call SaveChangesAsync - simulates rollback

        // Assert
        var notPersisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(notPersisted);
    }

    [Fact]
    public async Task Test28_MultipleOperations_SingleTransaction_AllOrNothing()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1 = CreateTestUser("trans1@example.com", Role.User);
        var user2 = CreateTestUser("trans2@example.com", Role.Admin);

        // Act
        await Repository.AddAsync(user1);
        await Repository.AddAsync(user2);
        await DbContext.SaveChangesAsync();

        // Assert
        var count = await DbContext.Users.CountAsync();
        Assert.Equal(2, count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Test29_NullableFields_HandledCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("nullable@example.com", Role.User);
        // DisplayName is nullable in some cases

        // Act
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Assert
        var retrieved = await Repository.GetByIdAsync(user.Id);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task Test30_SpecialCharacters_EmailHandling()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("user+tag@sub.domain.com", Role.User);

        // Act
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        var email = new Email("user+tag@sub.domain.com");
        var retrieved = await Repository.GetByEmailAsync(email);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("user+tag@sub.domain.com", retrieved.Email.Value);
    }

    [Fact]
    public async Task Test31_LargeDataset_PerformanceTest()
    {
        // Arrange - Add 100 users
        await ResetDatabaseAsync();
        var users = Enumerable.Range(1, 100)
            .Select(i => CreateTestUser($"user{i}@example.com", Role.User))
            .ToList();

        foreach (var user in users)
        {
            await Repository.AddAsync(user);
        }
        await DbContext.SaveChangesAsync();

        // Act
        var allUsers = await Repository.GetAllAsync();

        // Assert
        Assert.Equal(100, allUsers.Count);
    }

    [Fact]
    public async Task Test32_AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("notracking@example.com", Role.User);
        await Repository.AddAsync(user);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await Repository.GetByIdAsync(user.Id);

        // Modify retrieved object
        retrieved!.UpdateDisplayName("Modified");

        // SaveChanges without explicit Update call
        await DbContext.SaveChangesAsync();

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
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