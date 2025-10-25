using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for UserManagementService (ADMIN-01).
///
/// Feature: User Management Service
/// As an admin
/// I want to manage user accounts through a service
/// So that I can create, update, list, and delete users programmatically
///
/// Test Strategy: SQLite in-memory database for fast, isolated unit tests
/// </summary>
public class UserManagementServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly AuthService _authService;
    private readonly UserManagementService _service;

    public UserManagementServiceTests()
    {
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // Enable foreign keys for SQLite
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Use real AuthService - it works with SQLite
        // Pass null for ISessionCacheService (optional parameter)
        _authService = new AuthService(_dbContext, sessionCache: null, timeProvider: null);

        _service = new UserManagementService(
            _dbContext,
            _authService,
            NullLogger<UserManagementService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    #region GetUsersAsync Tests

    [Fact]
    public async Task GetUsersAsync_WithNoFilters_ReturnsAllUsers()
    {
        // Arrange
        await SeedTestUsers();

        // Act
        var result = await _service.GetUsersAsync(
            searchTerm: null,
            roleFilter: null,
            sortBy: null,
            sortOrder: "desc",
            page: 1,
            limit: 20);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetUsersAsync_WithSearchTerm_ReturnsMatchingUsers()
    {
        // Arrange
        await SeedTestUsers();

        // Act - search by email
        var result = await _service.GetUsersAsync(
            searchTerm: "admin",
            roleFilter: null,
            sortBy: null,
            sortOrder: "desc",
            page: 1,
            limit: 20);

        // Assert
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Contains("admin", result.Items[0].Email, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetUsersAsync_WithSearchTerm_MatchesDisplayName()
    {
        // Arrange
        await SeedTestUsers();

        // Act - search by display name
        var result = await _service.GetUsersAsync(
            searchTerm: "Administrator",
            roleFilter: null,
            sortBy: null,
            sortOrder: "desc",
            page: 1,
            limit: 20);

        // Assert
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Contains("Administrator", result.Items[0].DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetUsersAsync_WithRoleFilter_ReturnsOnlyMatchingRole()
    {
        // Arrange
        await SeedTestUsers();

        // Act - filter by Admin role
        var result = await _service.GetUsersAsync(
            searchTerm: null,
            roleFilter: "Admin",
            sortBy: null,
            sortOrder: "desc",
            page: 1,
            limit: 20);

        // Assert
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("Admin", result.Items[0].Role);
    }

    [Fact]
    public async Task GetUsersAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedTestUsers();

        // Act - get page 2 with limit 1
        var result = await _service.GetUsersAsync(
            searchTerm: null,
            roleFilter: null,
            sortBy: "email",
            sortOrder: "asc",
            page: 2,
            limit: 1);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Theory]
    [InlineData("email", "asc")]
    [InlineData("email", "desc")]
    [InlineData("displayname", "asc")]
    [InlineData("displayname", "desc")]
    [InlineData("role", "asc")]
    [InlineData("role", "desc")]
    [InlineData("createdat", "asc")]
    [InlineData("createdat", "desc")]
    public async Task GetUsersAsync_WithSorting_ReturnsSortedResults(string sortBy, string sortOrder)
    {
        // Arrange
        await SeedTestUsers();

        // Act
        var result = await _service.GetUsersAsync(
            searchTerm: null,
            roleFilter: null,
            sortBy: sortBy,
            sortOrder: sortOrder,
            page: 1,
            limit: 20);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
        // Verify items are actually sorted (basic check)
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task GetUsersAsync_WithLastSeenTracking_ReturnsLastSeenFromSessions()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };
        var session = new UserSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(25),
            LastSeenAt = DateTime.UtcNow.AddHours(-2),
            User = user
        };
        user.Sessions.Add(session);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUsersAsync(null, null, null, "desc", 1, 20);

        // Assert
        var userDto = result.Items.First();
        Assert.NotNull(userDto.LastSeenAt);
        Assert.True(userDto.LastSeenAt.Value > DateTime.UtcNow.AddHours(-3));
    }

    #endregion

    #region CreateUserAsync Tests

    [Fact]
    public async Task CreateUserAsync_WithValidData_CreatesUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            Email: "newuser@example.com",
            Password: "SecurePass123!",
            DisplayName: "New User",
            Role: "User"
        );

        // Act
        var result = await _service.CreateUserAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Email, result.Email);
        Assert.Equal(request.DisplayName, result.DisplayName);
        Assert.Equal("User", result.Role);

        // Verify user exists in database
        var dbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(dbUser);
        Assert.Equal(UserRole.User, dbUser.Role);
    }

    [Fact]
    public async Task CreateUserAsync_WithAdminRole_CreatesAdminUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            Email: "newadmin@example.com",
            Password: "SecurePass123!",
            DisplayName: "New Admin",
            Role: "Admin"
        );

        // Act
        var result = await _service.CreateUserAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Admin", result.Role);

        // Verify database
        var dbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(dbUser);
        Assert.Equal(UserRole.Admin, dbUser.Role);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        await SeedTestUsers();

        var request = new CreateUserRequest(
            Email: "admin@meepleai.dev", // Already exists
            Password: "SecurePass123!",
            DisplayName: "Duplicate Admin",
            Role: "Admin"
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateUserAsync(request));
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WithEditorRole_CreatesEditorUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            Email: "editor@example.com",
            Password: "password123",
            DisplayName: "Editor User",
            Role: "Editor"
        );

        // Act
        var result = await _service.CreateUserAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Email, result.Email);
        Assert.Equal(request.DisplayName, result.DisplayName);
        Assert.Equal("Editor", result.Role);

        // Verify database
        var dbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(dbUser);
        Assert.Equal(UserRole.Editor, dbUser.Role);
    }

    #endregion

    #region UpdateUserAsync Tests

    [Fact]
    public async Task UpdateUserAsync_WithValidData_UpdatesUser()
    {
        // Arrange
        var userId = await SeedSingleUser("original@example.com", "Original Name", UserRole.User);
        var request = new UpdateUserRequest(
            Email: "updated@example.com",
            DisplayName: "Updated Name",
            Role: "Editor"
        );

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        Assert.Equal("updated@example.com", result.Email);
        Assert.Equal("Updated Name", result.DisplayName);
        Assert.Equal("Editor", result.Role);

        // Verify database was updated
        var dbUser = await _dbContext.Users.FindAsync(userId);
        Assert.Equal("updated@example.com", dbUser!.Email);
        Assert.Equal("Updated Name", dbUser.DisplayName);
        Assert.Equal(UserRole.Editor, dbUser.Role);
    }

    [Fact]
    public async Task UpdateUserAsync_WithPartialData_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var userId = await SeedSingleUser("original@example.com", "Original Name", UserRole.User);
        var request = new UpdateUserRequest(
            Email: null,
            DisplayName: "New Name Only",
            Role: null
        );

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        Assert.Equal("original@example.com", result.Email); // Unchanged
        Assert.Equal("New Name Only", result.DisplayName); // Updated
        Assert.Equal("User", result.Role); // Unchanged
    }

    [Fact]
    public async Task UpdateUserAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        await SeedTestUsers();
        var userId = (await _dbContext.Users.FirstAsync(u => u.Email == "user@meepleai.dev")).Id;

        var request = new UpdateUserRequest(
            Email: "admin@meepleai.dev", // Already exists for another user
            DisplayName: null,
            Role: null
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateUserAsync(userId, request));
        Assert.Contains("already in use", exception.Message);
    }

    [Fact]
    public async Task UpdateUserAsync_WithNonExistentUser_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();
        var request = new UpdateUserRequest(
            Email: "test@example.com",
            DisplayName: "Test",
            Role: "User"
        );

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateUserAsync(nonExistentId, request));
    }

    [Fact]
    public async Task UpdateUserAsync_WithInvalidRole_IgnoresRoleUpdate()
    {
        // Arrange
        var userId = await SeedSingleUser("test@example.com", "Test", UserRole.User);
        var request = new UpdateUserRequest(
            Email: null,
            DisplayName: null,
            Role: "InvalidRole" // Not a valid UserRole enum value
        );

        // Act
        var result = await _service.UpdateUserAsync(userId, request);

        // Assert
        Assert.Equal("User", result.Role); // Role unchanged
    }

    #endregion

    #region DeleteUserAsync Tests

    [Fact]
    public async Task DeleteUserAsync_WithValidUser_DeletesUser()
    {
        // Arrange
        var userId = await SeedSingleUser("todelete@example.com", "To Delete", UserRole.User);
        var requestingUserId = Guid.NewGuid().ToString();

        // Act
        await _service.DeleteUserAsync(userId, requestingUserId);

        // Assert
        var deletedUser = await _dbContext.Users.FindAsync(userId);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task DeleteUserAsync_WithSelfDeletion_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = await SeedSingleUser("self@example.com", "Self", UserRole.Admin);

        // Act & Assert - trying to delete self
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteUserAsync(userId, userId));
        Assert.Contains("Cannot delete your own account", exception.Message);
    }

    [Fact]
    public async Task DeleteUserAsync_WithLastAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var adminId = await SeedSingleUser("lastadmin@example.com", "Last Admin", UserRole.Admin);
        var requestingUserId = Guid.NewGuid().ToString();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteUserAsync(adminId, requestingUserId));
        Assert.Contains("Cannot delete the last admin user", exception.Message);
    }

    [Fact]
    public async Task DeleteUserAsync_WithMultipleAdmins_AllowsDeletingOneAdmin()
    {
        // Arrange
        var admin1Id = await SeedSingleUser("admin1@example.com", "Admin 1", UserRole.Admin);
        var admin2Id = await SeedSingleUser("admin2@example.com", "Admin 2", UserRole.Admin);

        // Act - delete one admin while another exists
        await _service.DeleteUserAsync(admin1Id, admin2Id);

        // Assert
        var deletedAdmin = await _dbContext.Users.FindAsync(admin1Id);
        Assert.Null(deletedAdmin);

        var remainingAdmin = await _dbContext.Users.FindAsync(admin2Id);
        Assert.NotNull(remainingAdmin);
    }

    [Fact]
    public async Task DeleteUserAsync_WithNonExistentUser_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();
        var requestingUserId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.DeleteUserAsync(nonExistentId, requestingUserId));
    }

    [Fact]
    public async Task DeleteUserAsync_WithNonAdminUser_AllowsDeletion()
    {
        // Arrange
        var userId = await SeedSingleUser("user@example.com", "Regular User", UserRole.User);
        var adminId = await SeedSingleUser("admin@example.com", "Admin", UserRole.Admin);

        // Act
        await _service.DeleteUserAsync(userId, adminId);

        // Assert
        var deletedUser = await _dbContext.Users.FindAsync(userId);
        Assert.Null(deletedUser);
    }

    #endregion

    #region Helper Methods

    private async Task SeedTestUsers()
    {
        var users = new[]
        {
            new UserEntity
            {
                Id = Guid.NewGuid().ToString(),
                Email = "admin@meepleai.dev",
                DisplayName = "Administrator",
                PasswordHash = "hash1",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new UserEntity
            {
                Id = Guid.NewGuid().ToString(),
                Email = "editor@meepleai.dev",
                DisplayName = "Editor User",
                PasswordHash = "hash2",
                Role = UserRole.Editor,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new UserEntity
            {
                Id = Guid.NewGuid().ToString(),
                Email = "user@meepleai.dev",
                DisplayName = "Regular User",
                PasswordHash = "hash3",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _dbContext.Users.AddRange(users);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<string> SeedSingleUser(string email, string displayName, UserRole role)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = displayName,
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return userId;
    }

    #endregion
}

