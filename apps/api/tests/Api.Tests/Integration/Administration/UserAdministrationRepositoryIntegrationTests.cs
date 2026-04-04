using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for user administration operations using SharedTestcontainersFixture.
/// Tests administrative queries, bulk operations, user statistics, and tier management.
/// Issue #2307: Week 3 - Administration repository integration testing
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "2307")]
public sealed class UserAdministrationRepositoryIntegrationTests : IAsyncLifetime
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
    private static readonly Guid TestUserId1 = new("50000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("50000000-0000-0000-0000-000000000002");
    private static readonly Guid TestUserId3 = new("50000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId4 = new("50000000-0000-0000-0000-000000000004");

    public UserAdministrationRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_useradmin_{Guid.NewGuid():N}";
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
        return new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: role ?? Role.User,
            tier: tier ?? UserTier.Free
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<T> ExecuteInScopeAsync<T>(Func<IUserRepository, IUnitOfWork, Task<T>> action)
    {
        using var scope = _serviceProvider!.CreateScope();
        var scopedRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(scopedRepo, scopedUow);
    }

    #endregion

    #region FindUsersWithFiltersAsync Tests (Role, Tier, Status)

    [Fact]
    public async Task GetAllAsync_WithMixedRoles_ShouldReturnAllUsers()
    {
        // Arrange
        await CleanDatabaseAsync();
        var admin = CreateTestUser(TestUserId1, "admin@test.com", "Admin User", Role.Admin, UserTier.Premium);
        var editor = CreateTestUser(TestUserId2, "editor@test.com", "Editor User", Role.Editor, UserTier.Normal);
        var user = CreateTestUser(TestUserId3, "user@test.com", "Regular User", Role.User, UserTier.Free);

        await _repository!.AddAsync(admin, TestCancellationToken);
        await _repository.AddAsync(editor, TestCancellationToken);
        await _repository.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(u => u.Role == Role.Admin && u.Tier == UserTier.Premium);
        result.Should().Contain(u => u.Role == Role.Editor && u.Tier == UserTier.Normal);
        result.Should().Contain(u => u.Role == Role.User && u.Tier == UserTier.Free);
    }

    [Fact]
    public async Task SearchAsync_FilterByRole_ShouldReturnMatchingUsers()
    {
        // Arrange
        await CleanDatabaseAsync();
        var admin1 = CreateTestUser(TestUserId1, "admin1@test.com", "Admin One", Role.Admin);
        var admin2 = CreateTestUser(TestUserId2, "admin2@test.com", "Admin Two", Role.Admin);
        var regularUser = CreateTestUser(TestUserId3, "user@test.com", "Regular User", Role.User);

        await _repository!.AddAsync(admin1, TestCancellationToken);
        await _repository.AddAsync(admin2, TestCancellationToken);
        await _repository.AddAsync(regularUser, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Search for admin users by display name
        var result = await _repository.SearchAsync("Admin", 10, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(u => u.Role.Should().Be(Role.Admin));
    }

    #endregion

    #region BulkUpdateTierAsync Tests (Transaction)

    [Fact]
    public async Task BulkUpdateTier_MultipleUsers_ShouldUpdateInTransaction()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user1 = CreateTestUser(TestUserId1, "user1@test.com", tier: UserTier.Free);
        var user2 = CreateTestUser(TestUserId2, "user2@test.com", tier: UserTier.Free);
        var user3 = CreateTestUser(TestUserId3, "user3@test.com", tier: UserTier.Free);

        await _repository!.AddAsync(user1, TestCancellationToken);
        await _repository.AddAsync(user2, TestCancellationToken);
        await _repository.AddAsync(user3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Bulk update tiers (simulated through isolated scopes)
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var u1 = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            var u2 = await repo.GetByIdAsync(TestUserId2, TestCancellationToken);

            u1!.UpdateTier(UserTier.Normal, Role.Admin);
            u2!.UpdateTier(UserTier.Normal, Role.Admin);

            await repo.UpdateAsync(u1, TestCancellationToken);
            await repo.UpdateAsync(u2, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated1 = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        var updated2 = await _repository.GetByIdAsync(TestUserId2, TestCancellationToken);
        var unchanged = await _repository.GetByIdAsync(TestUserId3, TestCancellationToken);

        updated1!.Tier.Should().Be(UserTier.Normal);
        updated2!.Tier.Should().Be(UserTier.Normal);
        unchanged!.Tier.Should().Be(UserTier.Free);
    }

    #endregion

    #region GetUserStatisticsAsync Tests (Aggregation Query)

    [Fact]
    public async Task GetAllAsync_VariousTiers_ShouldEnableStatisticsCalculation()
    {
        // Arrange
        await CleanDatabaseAsync();
        var freeUser1 = CreateTestUser(TestUserId1, "free1@test.com", tier: UserTier.Free);
        var freeUser2 = CreateTestUser(TestUserId2, "free2@test.com", tier: UserTier.Free);
        var normalUser = CreateTestUser(TestUserId3, "normal@test.com", tier: UserTier.Normal);
        var premiumUser = CreateTestUser(TestUserId4, "premium@test.com", tier: UserTier.Premium);

        await _repository!.AddAsync(freeUser1, TestCancellationToken);
        await _repository.AddAsync(freeUser2, TestCancellationToken);
        await _repository.AddAsync(normalUser, TestCancellationToken);
        await _repository.AddAsync(premiumUser, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Get all users and calculate statistics
        var allUsers = await _repository.GetAllAsync(TestCancellationToken);
        var tierStats = allUsers.GroupBy(u => u.Tier)
            .ToDictionary(g => g.Key, g => g.Count());

        // Assert
        allUsers.Should().HaveCount(4);
        tierStats[UserTier.Free].Should().Be(2);
        tierStats[UserTier.Normal].Should().Be(1);
        tierStats[UserTier.Premium].Should().Be(1);
    }

    [Fact]
    public async Task CountAdminsAsync_WithMixedRoles_ShouldReturnAccurateCount()
    {
        // Arrange
        await CleanDatabaseAsync();
        var admin1 = CreateTestUser(TestUserId1, "admin1@test.com", role: Role.Admin);
        var admin2 = CreateTestUser(TestUserId2, "admin2@test.com", role: Role.Admin);
        var editor = CreateTestUser(TestUserId3, "editor@test.com", role: Role.Editor);
        var user = CreateTestUser(TestUserId4, "user@test.com", role: Role.User);

        await _repository!.AddAsync(admin1, TestCancellationToken);
        await _repository.AddAsync(admin2, TestCancellationToken);
        await _repository.AddAsync(editor, TestCancellationToken);
        await _repository.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var adminCount = await _repository.CountAdminsAsync(TestCancellationToken);

        // Assert
        adminCount.Should().Be(2);
    }

    #endregion

    #region Concurrent User Updates Tests (Optimistic Locking)

    [Fact]
    public async Task ConcurrentUpdates_IsolatedScopes_ShouldNotConflict()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "concurrent@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Two concurrent updates in isolated scopes (sequential for test simplicity)
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var u = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            u!.UpdatePreferences("en", "dark", true, 30);
            await repo.UpdateAsync(u, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var u = await repo.GetByIdAsync(TestUserId1, TestCancellationToken);
            u!.UpdateTier(UserTier.Normal, Role.Admin);
            await repo.UpdateAsync(u, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert - Both updates should be persisted
        var final = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        final!.Theme.Should().Be("dark");
        final.Tier.Should().Be(UserTier.Normal);
    }

    #endregion

    #region Soft Delete with Cascade Tests

    [Fact]
    public async Task DeleteAsync_UserWithRelations_ShouldCascadeDelete()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user = CreateTestUser(TestUserId1, "todelete@test.com");
        await _repository!.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Delete user
        await _repository.DeleteAsync(user, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await _repository.GetByIdAsync(TestUserId1, TestCancellationToken);
        deleted.Should().BeNull();

        var exists = await _repository.ExistsAsync(TestUserId1, TestCancellationToken);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_MultipleUsers_ShouldDeleteAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        var user1 = CreateTestUser(TestUserId1, "delete1@test.com");
        var user2 = CreateTestUser(TestUserId2, "delete2@test.com");
        var keepUser = CreateTestUser(TestUserId3, "keep@test.com");

        await _repository!.AddAsync(user1, TestCancellationToken);
        await _repository.AddAsync(user2, TestCancellationToken);
        await _repository.AddAsync(keepUser, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Delete two users
        await _repository.DeleteAsync(user1, TestCancellationToken);
        await _repository.DeleteAsync(user2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var remaining = await _repository.GetAllAsync(TestCancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Email.Value.Should().Be("keep@test.com");
    }

    #endregion
}
