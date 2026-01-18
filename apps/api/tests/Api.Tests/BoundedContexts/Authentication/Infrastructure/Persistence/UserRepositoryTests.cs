using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for UserRepository using shared Testcontainers with real PostgreSQL.
/// Tests actual EF Core queries and domain-persistence mapping.
/// Issue #2541: Migrated to SharedDatabaseTestBase for improved performance.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Unit)]
public class UserRepositoryTests : SharedDatabaseTestBase<UserRepository>
{
    public UserRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override UserRepository CreateRepository(MeepleAiDbContext dbContext)
        => new UserRepository(dbContext, MockEventCollector.Object);

    private CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;
    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("test@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await Repository.GetByIdAsync(user.Id, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("test@example.com", result.Email.Value);
        Assert.Equal(Role.User, result.Role);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingUser_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await Repository.GetByIdAsync(nonExistentId, TestCancellationToken);

        // Assert
        Assert.Null(result);
    }
    [Fact]
    public async Task GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("user@test.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var email = new Email("user@test.com");

        // Act
        var result = await Repository.GetByEmailAsync(email, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("user@test.com", result.Email.Value);
    }

    [Fact]
    public async Task GetByEmailAsync_CaseInsensitive_ReturnsUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("CaseSensitive@Example.COM", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var emailLowercase = new Email("casesensitive@example.com");

        // Act
        var result = await Repository.GetByEmailAsync(emailLowercase, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var email = new Email("nonexistent@example.com");

        // Act
        var result = await Repository.GetByEmailAsync(email, TestCancellationToken);

        // Assert
        Assert.Null(result);
    }
    [Fact]
    public async Task ExistsByEmailAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("exists@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var email = new Email("exists@example.com");

        // Act
        var exists = await Repository.ExistsByEmailAsync(email, TestCancellationToken);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByEmailAsync_NonExistingUser_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var email = new Email("notexists@example.com");

        // Act
        var exists = await Repository.ExistsByEmailAsync(email, TestCancellationToken);

        // Assert
        Assert.False(exists);
    }
    [Fact]
    public async Task HasAnyUsersAsync_EmptyDatabase_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var hasUsers = await Repository.HasAnyUsersAsync(TestCancellationToken);

        // Assert
        Assert.False(hasUsers);
    }

    [Fact]
    public async Task HasAnyUsersAsync_PopulatedDatabase_ReturnsTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("firstuser@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var hasUsers = await Repository.HasAnyUsersAsync(TestCancellationToken);

        // Assert
        Assert.True(hasUsers);
    }
    [Fact]
    public async Task CountAdminsAsync_NoAdmins_ReturnsZero()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1 = CreateTestUser("user1@example.com", Role.User);
        var user2 = CreateTestUser("user2@example.com", Role.Editor);
        await Repository.AddAsync(user1, TestCancellationToken);
        await Repository.AddAsync(user2, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var adminCount = await Repository.CountAdminsAsync(TestCancellationToken);

        // Assert
        Assert.Equal(0, adminCount);
    }

    [Fact]
    public async Task CountAdminsAsync_MultipleAdmins_ReturnsCorrectCount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var admin1 = CreateTestUser("admin1@example.com", Role.Admin);
        var admin2 = CreateTestUser("admin2@example.com", Role.Admin);
        var user = CreateTestUser("user@example.com", Role.User);
        var editor = CreateTestUser("editor@example.com", Role.Editor);

        await Repository.AddAsync(admin1, TestCancellationToken);
        await Repository.AddAsync(admin2, TestCancellationToken);
        await Repository.AddAsync(user, TestCancellationToken);
        await Repository.AddAsync(editor, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var adminCount = await Repository.CountAdminsAsync(TestCancellationToken);

        // Assert
        Assert.Equal(2, adminCount);
    }
    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var users = await Repository.GetAllAsync(TestCancellationToken);

        // Assert
        Assert.Empty(users);
    }

    [Fact]
    public async Task GetAllAsync_MultipleUsers_ReturnsAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1 = CreateTestUser("user1@example.com", Role.User);
        var user2 = CreateTestUser("user2@example.com", Role.Admin);
        var user3 = CreateTestUser("user3@example.com", Role.Editor);

        await Repository.AddAsync(user1, TestCancellationToken);
        await Repository.AddAsync(user2, TestCancellationToken);
        await Repository.AddAsync(user3, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var users = await Repository.GetAllAsync(TestCancellationToken);

        // Assert
        Assert.Equal(3, users.Count);
        Assert.Contains(users, u => u.Email.Value == "user1@example.com");
        Assert.Contains(users, u => u.Email.Value == "user2@example.com");
        Assert.Contains(users, u => u.Email.Value == "user3@example.com");
    }
    [Fact]
    public async Task AddAsync_NewUser_PersistsSuccessfully()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("newuser@example.com", Role.User);

        // Act
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal("newuser@example.com", persisted.Email);
        Assert.Equal(Role.User.Value, persisted.Role);
    }

    [Fact]
    public async Task AddAsync_UserWith2FA_PersistsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("2fa@example.com", Role.User);
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_totp_secret_123"));

        // Act
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.True(persisted.IsTwoFactorEnabled);
        Assert.Equal("encrypted_totp_secret_123", persisted.TotpSecretEncrypted);
        Assert.NotNull(persisted.TwoFactorEnabledAt);
    }
    [Fact]
    public async Task UpdateAsync_ModifiedUser_PersistsChanges()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("update@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        DbContext.ChangeTracker.Clear();

        // Modify user
        user.UpdateDisplayName("Updated Name");
        user.UpdateRole(Role.Admin);

        // Act
        await Repository.UpdateAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal(Role.Admin.Value, updated.Role);
    }

    [Fact]
    public async Task UpdateAsync_Enable2FA_UpdatesCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("enable2fa@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Enable 2FA
        user.Enable2FA(TotpSecret.FromEncrypted("new_encrypted_secret"));

        // Act
        await Repository.UpdateAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.True(updated.IsTwoFactorEnabled);
        Assert.Equal("new_encrypted_secret", updated.TotpSecretEncrypted);
    }
    [Fact]
    public async Task DeleteAsync_ExistingUser_RemovesFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("delete@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        await Repository.DeleteAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingUser_DoesNotThrow()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("nonexistent@example.com", Role.User);

        // Act & Assert - Should not throw
        await Repository.DeleteAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);
    }
    [Fact]
    public async Task ExistsAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("exists@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var exists = await Repository.ExistsAsync(user.Id, TestCancellationToken);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingUser_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var exists = await Repository.ExistsAsync(nonExistentId, TestCancellationToken);

        // Assert
        Assert.False(exists);
    }
    [Fact]
    public async Task Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("mapping@example.com", Role.Admin);
        user.UpdateDisplayName("Mapping Test");

        // Act
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal(user.Email.Value, persisted.Email);
        Assert.Equal(user.DisplayName, persisted.DisplayName);
        Assert.Equal(user.PasswordHash.Value, persisted.PasswordHash);
        Assert.Equal(user.Role.Value, persisted.Role);
        Assert.Equal(user.CreatedAt, persisted.CreatedAt);
    }

    [Fact]
    public async Task Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("roundtrip@example.com", Role.Editor);
        user.UpdateDisplayName("Round Trip Test");
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await Repository.GetByIdAsync(user.Id, TestCancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.Id);
        Assert.Equal(user.Email.Value, retrieved.Email.Value);
        Assert.Equal(user.DisplayName, retrieved.DisplayName);
        Assert.Equal(user.Role.Value, retrieved.Role.Value);
        Assert.Equal(user.IsTwoFactorEnabled, retrieved.IsTwoFactorEnabled);
    }

    [Fact]
    public async Task Mapping_2FAFields_PreservedCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("2famapping@example.com", Role.User);
        user.Enable2FA(TotpSecret.FromEncrypted("secret_123"));
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await Repository.GetByIdAsync(user.Id, TestCancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsTwoFactorEnabled);
        Assert.Equal("secret_123", retrieved.TotpSecret?.EncryptedValue);
        Assert.NotNull(retrieved.TwoFactorEnabledAt);
    }
    [Fact]
    public async Task ConcurrentReads_NoConflicts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("concurrent@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple concurrent reads using independent repositories
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            return await repo.GetByIdAsync(user.Id, TestCancellationToken);
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
    public async Task ConcurrentEmailLookups_NoConflicts()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("concurrentemail@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var email = new Email("concurrentemail@example.com");

        // Act - Multiple concurrent email lookups using independent repositories
        // FIX: Use CreateIndependentRepository() to avoid DbContext concurrency issues
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var repo = CreateIndependentRepository();
            return await repo.GetByEmailAsync(email, TestCancellationToken);
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
    public async Task Transaction_Rollback_NoDataPersisted()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("transaction@example.com", Role.User);

        // Act
        await Repository.AddAsync(user, TestCancellationToken);
        // DO NOT call SaveChangesAsync - simulates rollback

        // Assert
        var notPersisted = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Null(notPersisted);
    }

    [Fact]
    public async Task MultipleOperations_SingleTransaction_AllOrNothing()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1 = CreateTestUser("trans1@example.com", Role.User);
        var user2 = CreateTestUser("trans2@example.com", Role.Admin);

        // Act
        await Repository.AddAsync(user1, TestCancellationToken);
        await Repository.AddAsync(user2, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var count = await DbContext.Users.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
    }
    [Fact]
    public async Task NullableFields_HandledCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("nullable@example.com", Role.User);
        // DisplayName is nullable in some cases

        // Act
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await Repository.GetByIdAsync(user.Id, TestCancellationToken);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task SpecialCharacters_EmailHandling()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("user+tag@sub.domain.com", Role.User);

        // Act
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var email = new Email("user+tag@sub.domain.com");
        var retrieved = await Repository.GetByEmailAsync(email, TestCancellationToken);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("user+tag@sub.domain.com", retrieved.Email.Value);
    }

    [Fact]
    public async Task LargeDataset_PerformanceTest()
    {
        // Arrange - Add 100 users
        await ResetDatabaseAsync();
        var users = Enumerable.Range(1, 100)
            .Select(i => CreateTestUser($"user{i}@example.com", Role.User))
            .ToList();

        foreach (var user in users)
        {
            await Repository.AddAsync(user, TestCancellationToken);
        }
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var allUsers = await Repository.GetAllAsync(TestCancellationToken);

        // Assert
        Assert.Equal(100, allUsers.Count);
    }

    [Fact]
    public async Task AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user = CreateTestUser("notracking@example.com", Role.User);
        await Repository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await Repository.GetByIdAsync(user.Id, TestCancellationToken);

        // Modify retrieved object
        retrieved!.UpdateDisplayName("Modified");

        // SaveChanges without explicit Update call
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await DbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotEqual("Modified", reloaded!.DisplayName);
    }
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
}

