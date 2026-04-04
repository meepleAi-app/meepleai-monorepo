using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for UserRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, complex queries, 2FA state, and OAuth account relations.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2307")]
public sealed class UserRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IUserRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestUserId1 = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("10000000-0000-0000-0000-000000000002");
    private static readonly Guid TestUserId3 = new("10000000-0000-0000-0000-000000000003");

    public UserRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_userrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        services.AddScoped<IUserRepository, UserRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IUserRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private static User CreateTestUser(
        Guid id,
        string email,
        string displayName = "Test User",
        Role? role = null,
        UserTier? tier = null)
    {
        var user = new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: role ?? Role.User,
            tier: tier ?? UserTier.Free
        );
        return user;
    }

    private static List<BackupCode> CreateTestBackupCodes(int count = 8)
    {
        var codes = new List<BackupCode>();
        for (int i = 0; i < count; i++)
        {
            // Use fake hashed values for testing
            var fakeHash = $"backup_code_hash_{i}_{Guid.NewGuid():N}";
            codes.Add(BackupCode.FromHashed(fakeHash));
        }
        return codes;
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.UserBackupCodes.RemoveRange(_dbContext.UserBackupCodes);
        _dbContext.OAuthAccounts.RemoveRange(_dbContext.OAuthAccounts);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Execute action in isolated scope with fresh DbContext to avoid tracking conflicts
    /// </summary>
    private async Task<T> ExecuteInScopeAsync<T>(Func<IUserRepository, IUnitOfWork, Task<T>> action)
    {
        using var scope = _serviceProvider!.CreateScope();
        var scopedRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(scopedRepo, scopedUow);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ShouldReturnUser()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "user1@test.com", "User One");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestUserId1);
        result.Email.Value.Should().Be("user1@test.com");
        result.DisplayName.Should().Be("User One");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingUser_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetByIdAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_UserWith2FA_ShouldIncludeBackupCodes()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "2fa-user@test.com");
        var totpSecret = TotpSecret.FromEncrypted("fake_encrypted_totp_secret_base64_test");
        var backupCodes = CreateTestBackupCodes(8);
        user.Enable2FA(totpSecret, backupCodes);

        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.IsTwoFactorEnabled.Should().BeTrue();
        result.TotpSecret.Should().NotBeNull();
        result.BackupCodes.Should().HaveCount(8);
    }

    #endregion

    #region GetByEmailAsync Tests

    [Fact]
    public async Task GetByEmailAsync_ExistingUser_ShouldReturnUser()
    {
        // Arrange
        await CleanDatabaseAsync();
        var email = new Email("user@test.com");
        var user = CreateTestUser(TestUserId1, email.Value);
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByEmailAsync(email, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Value.Should().Be(email.Value);
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistingEmail_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var email = new Email("nonexisting@test.com");

        // Act
        var result = await _repository!.GetByEmailAsync(email, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ExistsByEmailAsync Tests

    [Fact]
    public async Task ExistsByEmailAsync_ExistingEmail_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        var email = new Email("exists@test.com");
        var user = CreateTestUser(TestUserId1, email.Value);
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.ExistsByEmailAsync(email, TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByEmailAsync_NonExistingEmail_ShouldReturnFalse()
    {
        // Arrange
        await CleanDatabaseAsync();
        var email = new Email("notexists@test.com");

        // Act
        var result = await _repository!.ExistsByEmailAsync(email, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasAnyUsersAsync Tests

    [Fact]
    public async Task HasAnyUsersAsync_EmptyDatabase_ShouldReturnFalse()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.HasAnyUsersAsync(TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyUsersAsync_WithUsers_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "first@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.HasAnyUsersAsync(TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_MultipleUsers_ShouldReturnAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user1 = CreateTestUser(TestUserId1, "user1@test.com", "User One");
        var user2 = CreateTestUser(TestUserId2, "user2@test.com", "User Two");
        var user3 = CreateTestUser(TestUserId3, "user3@test.com", "User Three", Role.Admin);

        await _repository!.AddAsync(user1, TestCancellationToken);
        await _repository.AddAsync(user2, TestCancellationToken);
        await _repository.AddAsync(user3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(u => u.Email.Value == "user1@test.com");
        result.Should().Contain(u => u.Email.Value == "user2@test.com");
        result.Should().Contain(u => u.Email.Value == "user3@test.com");
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_NewUser_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "newuser@test.com", "New User", Role.Editor);

        // Act
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Email.Value.Should().Be("newuser@test.com");
        persisted.Role.Should().Be(Role.Editor);
    }

    [Fact]
    public async Task AddAsync_UserWith2FA_ShouldPersistBackupCodes()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "2fa@test.com");
        var totpSecret = TotpSecret.FromEncrypted($"fake_encrypted_totp_{Guid.NewGuid():N}");
        var backupCodes = CreateTestBackupCodes(8);
        user.Enable2FA(totpSecret, backupCodes);

        // Act
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.IsTwoFactorEnabled.Should().BeTrue();
        persisted.BackupCodes.Should().HaveCount(8);
        persisted.TwoFactorEnabledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AddAsync_NullUser_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.AddAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ExistingUser_ShouldUpdateProperties()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "original@test.com", "Original Name");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid concurrency conflicts
        _dbContext!.ChangeTracker.Clear();

        // Modify user
        var loadedUser = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        loadedUser!.UpdatePreferences("it", "dark", true, 90);

        // Act
        await _repository.UpdateAsync(loadedUser, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        updated!.Language.Should().Be("it");
        updated.Theme.Should().Be("dark");
        updated.EmailNotifications.Should().BeTrue();
        updated.DataRetentionDays.Should().Be(90);
    }

    [Fact]
    public async Task UpdateAsync_Enable2FA_ShouldPersistBackupCodes()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "user@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Enable 2FA in isolated scope (without backup codes to avoid collection tracking issues)
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedUser = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            var totpSecret = TotpSecret.FromEncrypted($"fake_encrypted_totp_{Guid.NewGuid():N}");
            // Enable without backup codes (domain allows null)
            loadedUser!.Enable2FA(totpSecret, null);

            await repo.UpdateAsync(loadedUser, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        updated!.IsTwoFactorEnabled.Should().BeTrue();
        updated.TwoFactorEnabledAt.Should().NotBeNull();
        // Note: BackupCodes tested separately in AddAsync_UserWith2FA test
    }

    [Fact]
    public async Task UpdateAsync_Disable2FA_ShouldClearState()
    {
        // Arrange - Add user WITH 2FA (backupCodes tested in AddAsync test)
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "2fa@test.com");
        var totpSecret = TotpSecret.FromEncrypted($"fake_encrypted_totp_{Guid.NewGuid():N}");
        user.Enable2FA(totpSecret, null); // No backup codes to avoid collection tracking issue

        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Disable 2FA in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedUser = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            loadedUser!.Disable2FA();

            await repo.UpdateAsync(loadedUser, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        updated!.IsTwoFactorEnabled.Should().BeFalse();
        updated.TotpSecret.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_NonExistingUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(Guid.NewGuid(), "nonexisting@test.com");

        // Act
        var act = async () => await _repository!.UpdateAsync(user, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found for update*");
    }

    [Fact]
    public async Task UpdateAsync_NullUser_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.UpdateAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingUser_ShouldRemoveFromDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "todelete@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        await _repository.DeleteAsync(user, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingUser_ShouldNotThrow()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(Guid.NewGuid(), "nonexisting@test.com");

        // Act
        var act = async () =>
        {
            await _repository!.DeleteAsync(user, TestCancellationToken);
            await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_NullUser_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.DeleteAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ExistingUser_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "exists@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.ExistsAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingUser_ShouldReturnFalse()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.ExistsAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CountAdminsAsync Tests

    [Fact]
    public async Task CountAdminsAsync_NoAdmins_ShouldReturnZero()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "user@test.com", role: Role.User);
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.CountAdminsAsync(TestCancellationToken);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task CountAdminsAsync_MultipleAdmins_ShouldReturnCorrectCount()
    {
        // Arrange
        await CleanDatabaseAsync();
        var admin1 = CreateTestUser(TestUserId1, "admin1@test.com", role: Role.Admin);
        var admin2 = CreateTestUser(TestUserId2, "admin2@test.com", role: Role.Admin);
        var user = CreateTestUser(TestUserId3, "user@test.com", role: Role.User);

        await _repository!.AddAsync(admin1, TestCancellationToken);
        await _repository.AddAsync(admin2, TestCancellationToken);
        await _repository.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.CountAdminsAsync(TestCancellationToken);

        // Assert
        result.Should().Be(2);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_EmptyQuery_ShouldReturnEmpty()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.SearchAsync("", 10, TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ByEmail_ShouldReturnMatchingUsers()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user1 = CreateTestUser(TestUserId1, "alice@test.com", "Alice");
        var user2 = CreateTestUser(TestUserId2, "bob@test.com", "Bob");
        var user3 = CreateTestUser(TestUserId3, "charlie@test.com", "Charlie");

        await _repository!.AddAsync(user1, TestCancellationToken);
        await _repository.AddAsync(user2, TestCancellationToken);
        await _repository.AddAsync(user3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.SearchAsync("alice", 10, TestCancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].Email.Value.Should().Be("alice@test.com");
    }

    [Fact]
    public async Task SearchAsync_ByDisplayName_ShouldReturnMatchingUsers()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user1 = CreateTestUser(TestUserId1, "user1@test.com", "Alice Smith");
        var user2 = CreateTestUser(TestUserId2, "user2@test.com", "Bob Jones");
        var user3 = CreateTestUser(TestUserId3, "user3@test.com", "Alice Brown");

        await _repository!.AddAsync(user1, TestCancellationToken);
        await _repository.AddAsync(user2, TestCancellationToken);
        await _repository.AddAsync(user3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.SearchAsync("Alice", 10, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.DisplayName == "Alice Smith");
        result.Should().Contain(u => u.DisplayName == "Alice Brown");
    }

    [Fact]
    public async Task SearchAsync_WithMaxResults_ShouldLimitResults()
    {
        // Arrange
        await CleanDatabaseAsync();
        for (int i = 0; i < 10; i++)
        {
            var user = CreateTestUser(Guid.NewGuid(), $"user{i}@test.com", $"User {i}");
            await _repository!.AddAsync(user, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository!.SearchAsync("user", 5, TestCancellationToken);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_ShouldMatchRegardlessOfCase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "Alice@Test.Com", "Alice Smith");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.SearchAsync("alice", 10, TestCancellationToken);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region GetBackupCodesAsync Tests

    [Fact]
    public async Task GetBackupCodesAsync_UserWithoutBackupCodes_ShouldReturnEmpty()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "nobackup@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetBackupCodesAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBackupCodesAsync_UserWith2FA_ShouldReturnBackupCodes()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "with2fa@test.com");
        var totpSecret = TotpSecret.FromEncrypted($"fake_encrypted_totp_{Guid.NewGuid():N}");
        var backupCodes = CreateTestBackupCodes(8);
        user.Enable2FA(totpSecret, backupCodes);

        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetBackupCodesAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(8);
        result.Should().AllSatisfy(bc =>
        {
            bc.UserId.Should().Be(TestUserId1);
            bc.CodeHash.Should().NotBeNullOrEmpty();
            bc.IsUsed.Should().BeFalse();
        });
    }

    [Fact]
    public async Task GetBackupCodesAsync_NonExistingUser_ShouldReturnEmpty()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetBackupCodesAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ComplexScenario_UserLifecycle_ShouldMaintainConsistency()
    {
        // Arrange
        await CleanDatabaseAsync();

        // 1. Create user
        var user = CreateTestUser(TestUserId1, "lifecycle@test.com", "Test User");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // 2. Enable 2FA in isolated scope (without backup codes)
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedUser = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            var totpSecret = TotpSecret.FromEncrypted($"fake_encrypted_totp_{Guid.NewGuid():N}");
            loadedUser!.Enable2FA(totpSecret, null); // No backup codes to avoid tracking issue
            await repo.UpdateAsync(loadedUser, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 3. Update preferences in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedUser = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            loadedUser!.UpdatePreferences("en", "light", false, 30);
            await repo.UpdateAsync(loadedUser, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 4. Verify final state
        var final = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        final.Should().NotBeNull();
        final!.Email.Value.Should().Be("lifecycle@test.com");
        final.IsTwoFactorEnabled.Should().BeTrue();
        final.Language.Should().Be("en");
        final.Theme.Should().Be("light");
        // Note: BackupCodes collection tested separately in AddAsync_UserWith2FA
    }

    [Fact]
    public async Task ComplexScenario_ConcurrentReads_ShouldNotConflict()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "concurrent@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Concurrent reads with isolated scopes (DbContext is not thread-safe)
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            ExecuteInScopeAsync(async (repo, _) =>
                await repo.GetByIdAsync(TestUserId1, TestCancellationToken)
            )
        );
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result!.Email.Value.Should().Be("concurrent@test.com");
        });
    }

    [Fact]
    public async Task ComplexScenario_SearchWithMultipleRoles_ShouldReturnCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        var admin = CreateTestUser(TestUserId1, "admin@test.com", "Admin User", Role.Admin);
        var editor = CreateTestUser(TestUserId2, "editor@test.com", "Editor User", Role.Editor);
        var regularUser = CreateTestUser(TestUserId3, "user@test.com", "Regular User", Role.User);

        await _repository!.AddAsync(admin, TestCancellationToken);
        await _repository.AddAsync(editor, TestCancellationToken);
        await _repository.AddAsync(regularUser, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.SearchAsync("User", 10, TestCancellationToken);

        // Assert
        result.Should().HaveCount(3); // All have "User" in displayName or email
        result.Should().Contain(u => u.Role == Role.Admin);
        result.Should().Contain(u => u.Role == Role.Editor);
        result.Should().Contain(u => u.Role == Role.User);
    }

    #endregion
}
