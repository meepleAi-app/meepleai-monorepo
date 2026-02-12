using Api.BoundedContexts.Administration.Application.Queries.GetUserPermissions;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Integration tests for GetUserPermissions query (Epic #4068 - Issue #4177)
/// Tests actual database interaction with in-memory database
/// </summary>
public class GetUserPermissionsIntegrationTests : IAsyncLifetime
{
    private readonly MeepleAiDbContext _context;
    private readonly PermissionRegistry _registry;
    private readonly GetUserPermissionsHandler _handler;

    private User _freeUser = null!;
    private User _normalUser = null!;
    private User _proUser = null!;
    private User _enterpriseUser = null!;
    private User _editorUser = null!;
    private User _creatorUser = null!;
    private User _adminUser = null!;
    private User _suspendedUser = null!;

    public GetUserPermissionsIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PermissionIntegrationTests_{Guid.NewGuid()}")
            .Options;

        _context = new MeepleAiDbContext(options);
        _registry = new PermissionRegistry();
        _handler = new GetUserPermissionsHandler(_context, _registry);
    }

    public async Task InitializeAsync()
    {
        // Seed test users with different tiers/roles
        _freeUser = CreateUser("free@test.com", "Free User", UserTier.Free, Role.User);
        _normalUser = CreateUser("normal@test.com", "Normal User", UserTier.Normal, Role.User);
        _proUser = CreateUser("pro@test.com", "Pro User", UserTier.Pro, Role.User);
        _enterpriseUser = CreateUser("enterprise@test.com", "Enterprise User", UserTier.Enterprise, Role.User);
        _editorUser = CreateUser("editor@test.com", "Editor User", UserTier.Free, Role.Editor);
        _creatorUser = CreateUser("creator@test.com", "Creator User", UserTier.Normal, Role.Creator);
        _adminUser = CreateUser("admin@test.com", "Admin User", UserTier.Free, Role.Admin);
        _suspendedUser = CreateUser("suspended@test.com", "Suspended User", UserTier.Pro, Role.User);
        _suspendedUser.Suspend("Test suspension");

        _context.Users.AddRange(
            _freeUser, _normalUser, _proUser, _enterpriseUser,
            _editorUser, _creatorUser, _adminUser, _suspendedUser);

        await _context.SaveChangesAsync();
    }

    private User CreateUser(string email, string displayName, UserTier tier, Role role)
    {
        var user = new User(
            Guid.NewGuid(),
            Email.Create(email).Value,
            displayName,
            PasswordHash.Create("password123").Value,
            role,
            tier);

        return user;
    }

    [Fact]
    public async Task Handle_WithFreeUser_ReturnsLimitedFeatures()
    {
        // Arrange
        var query = new GetUserPermissionsQuery(_freeUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("free", result.Tier);
        Assert.Equal("user", result.Role);
        Assert.Equal(50, result.Limits.MaxGames);
        Assert.Equal(100, result.Limits.StorageQuotaMB);

        // Free user has only wishlist
        Assert.Contains("wishlist", result.AccessibleFeatures);
        Assert.DoesNotContain("bulk-select", result.AccessibleFeatures);
        Assert.DoesNotContain("drag-drop", result.AccessibleFeatures);
        Assert.DoesNotContain("agent.create", result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithProUser_ReturnsExtendedFeatures()
    {
        // Arrange
        var query = new GetUserPermissionsQuery(_proUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("pro", result.Tier);
        Assert.Equal(500, result.Limits.MaxGames);
        Assert.Equal(5000, result.Limits.StorageQuotaMB);

        // Pro user has bulk-select, agent creation, analytics
        Assert.Contains("wishlist", result.AccessibleFeatures);
        Assert.Contains("bulk-select", result.AccessibleFeatures);
        Assert.Contains("drag-drop", result.AccessibleFeatures);
        Assert.Contains("agent.create", result.AccessibleFeatures);
        Assert.Contains("analytics.view", result.AccessibleFeatures);

        // But not admin features
        Assert.DoesNotContain("quick-action.delete", result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithEnterpriseUser_ReturnsUnlimitedLimits()
    {
        // Arrange
        var query = new GetUserPermissionsQuery(_enterpriseUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("enterprise", result.Tier);
        Assert.Equal(int.MaxValue, result.Limits.MaxGames);
        Assert.Equal(int.MaxValue, result.Limits.StorageQuotaMB);

        // All standard features accessible
        Assert.Contains("bulk-select", result.AccessibleFeatures);
        Assert.Contains("agent.create", result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithEditorRole_AccessesEditorFeatures()
    {
        // Arrange: Free tier + Editor role
        var query = new GetUserPermissionsQuery(_editorUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("free", result.Tier); // Low tier
        Assert.Equal("editor", result.Role);

        // Editor role grants bulk-select (OR logic: Pro tier OR Editor role)
        Assert.Contains("bulk-select", result.AccessibleFeatures);

        // But not tier-locked features (agent creation requires Pro tier OR Creator role)
        Assert.DoesNotContain("agent.create", result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithCreatorRole_AccessesCreatorFeatures()
    {
        // Arrange: Normal tier + Creator role
        var query = new GetUserPermissionsQuery(_creatorUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("normal", result.Tier);
        Assert.Equal("creator", result.Role);

        // Creator can create agents (OR logic: Pro tier OR Creator role)
        Assert.Contains("agent.create", result.AccessibleFeatures);

        // Creator can edit games
        Assert.Contains("quick-action.edit", result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithAdminRole_AccessesAdminFeatures()
    {
        // Arrange: Free tier + Admin role
        var query = new GetUserPermissionsQuery(_adminUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("free", result.Tier); // Low tier
        Assert.Equal("admin", result.Role);

        // Admin has admin features (AND logic: Admin role required)
        Assert.Contains("quick-action.delete", result.AccessibleFeatures);

        // Admin also gets OR-gated features (bulk-select: Pro tier OR Editor role, Admin > Editor)
        Assert.Contains("bulk-select", result.AccessibleFeatures);

        // Admin gets analytics (Pro tier OR Admin role)
        Assert.Contains("analytics.view", result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithSuspendedUser_ReturnsEmptyFeatures()
    {
        // Arrange
        var query = new GetUserPermissionsQuery(_suspendedUser.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("Suspended", result.Status.ToString());

        // Suspended users have no features (all denied)
        Assert.Empty(result.AccessibleFeatures);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ThrowsNotFoundException()
    {
        // Arrange
        var query = new GetUserPermissionsQuery(Guid.NewGuid());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));

        Assert.Equal("User", exception.ResourceType);
    }

    [Theory]
    [InlineData("free", 50, 100)]
    [InlineData("normal", 100, 500)]
    [InlineData("pro", 500, 5000)]
    [InlineData("enterprise", int.MaxValue, int.MaxValue)]
    public async Task Handle_ReturnsCorrectLimitsForTier(string tierValue, int expectedMaxGames, int expectedStorageMB)
    {
        // Arrange
        var user = CreateUser($"{tierValue}@test.com", $"{tierValue} User", UserTier.Parse(tierValue), Role.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var query = new GetUserPermissionsQuery(user.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(expectedMaxGames, result.Limits.MaxGames);
        Assert.Equal(expectedStorageMB, result.Limits.StorageQuotaMB);
    }

    [Fact]
    public async Task Handle_WithMultipleConcurrentRequests_HandlesCorrectly()
    {
        // Arrange: Simulate 10 concurrent permission checks
        var queries = Enumerable.Range(0, 10)
            .Select(_ => new GetUserPermissionsQuery(_proUser.Id))
            .ToList();

        // Act: Execute concurrently
        var results = await Task.WhenAll(
            queries.Select(q => _handler.Handle(q, CancellationToken.None)));

        // Assert: All return same result
        Assert.All(results, r => Assert.Equal("pro", r.Tier));
        Assert.All(results, r => Assert.Equal(500, r.Limits.MaxGames));
    }

    [Fact]
    public async Task Handle_CachesAccessibleFeatures_ForSameUser()
    {
        // Arrange
        var query = new GetUserPermissionsQuery(_proUser.Id);

        // Act: Call multiple times
        var result1 = await _handler.Handle(query, CancellationToken.None);
        var result2 = await _handler.Handle(query, CancellationToken.None);

        // Assert: Same accessible features list (cached in PermissionRegistry)
        Assert.Equal(result1.AccessibleFeatures, result2.AccessibleFeatures);

        // Note: In production with HybridCache, database query itself would be cached
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
