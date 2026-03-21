using System.Security.Claims;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Security;

/// <summary>
/// Authorization Policy Tests for Admin Role System (Issue #3690).
/// Validates role-based authorization policies: RequireSuperAdmin, RequireAdminOrAbove, RequireEditorOrAbove.
///
/// Security Requirements:
/// 1. SuperAdmin has full system access
/// 2. Admin has operations/monitoring access but not global feature flags
/// 3. Editor has content management access only
/// 4. User has no admin access
///
/// Pattern: Unit testing authorization policies with ClaimsPrincipal
/// </summary>
[Trait("Category", TestCategories.Security)]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "3690")]
public sealed class RoleAuthorizationPolicyTests : IDisposable
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ServiceProvider _serviceProvider;

    public RoleAuthorizationPolicyTests()
    {
        var services = new ServiceCollection();

        // Configure authorization with the same policies as production
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireSuperAdmin", policy =>
                policy.RequireRole("SuperAdmin"));

            options.AddPolicy("RequireAdminOrAbove", policy =>
                policy.RequireRole("SuperAdmin", "Admin"));

            options.AddPolicy("RequireEditorOrAbove", policy =>
                policy.RequireRole("SuperAdmin", "Admin", "Editor"));
        });

        // Add required logging for authorization
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();
    }

    #region RequireSuperAdmin Policy Tests

    [Fact]
    public async Task RequireSuperAdmin_SuperAdminRole_ReturnsSuccess()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("SuperAdmin");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireSuperAdmin");

        // Assert
        result.Succeeded.Should().BeTrue("SuperAdmin should have access to RequireSuperAdmin policy");
    }

    [Fact]
    public async Task RequireSuperAdmin_AdminRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("Admin");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireSuperAdmin");

        // Assert
        result.Succeeded.Should().BeFalse("Admin should NOT have access to RequireSuperAdmin policy");
    }

    [Fact]
    public async Task RequireSuperAdmin_EditorRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("Editor");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireSuperAdmin");

        // Assert
        result.Succeeded.Should().BeFalse("Editor should NOT have access to RequireSuperAdmin policy");
    }

    [Fact]
    public async Task RequireSuperAdmin_UserRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("User");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireSuperAdmin");

        // Assert
        result.Succeeded.Should().BeFalse("User should NOT have access to RequireSuperAdmin policy");
    }

    [Fact]
    public async Task RequireSuperAdmin_NoRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithoutRole();

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireSuperAdmin");

        // Assert
        result.Succeeded.Should().BeFalse("Anonymous user should NOT have access to RequireSuperAdmin policy");
    }

    #endregion

    #region RequireAdminOrAbove Policy Tests

    [Fact]
    public async Task RequireAdminOrAbove_SuperAdminRole_ReturnsSuccess()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("SuperAdmin");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireAdminOrAbove");

        // Assert
        result.Succeeded.Should().BeTrue("SuperAdmin should have access to RequireAdminOrAbove policy");
    }

    [Fact]
    public async Task RequireAdminOrAbove_AdminRole_ReturnsSuccess()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("Admin");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireAdminOrAbove");

        // Assert
        result.Succeeded.Should().BeTrue("Admin should have access to RequireAdminOrAbove policy");
    }

    [Fact]
    public async Task RequireAdminOrAbove_EditorRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("Editor");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireAdminOrAbove");

        // Assert
        result.Succeeded.Should().BeFalse("Editor should NOT have access to RequireAdminOrAbove policy");
    }

    [Fact]
    public async Task RequireAdminOrAbove_UserRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("User");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireAdminOrAbove");

        // Assert
        result.Succeeded.Should().BeFalse("User should NOT have access to RequireAdminOrAbove policy");
    }

    [Fact]
    public async Task RequireAdminOrAbove_NoRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithoutRole();

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireAdminOrAbove");

        // Assert
        result.Succeeded.Should().BeFalse("Anonymous user should NOT have access to RequireAdminOrAbove policy");
    }

    #endregion

    #region RequireEditorOrAbove Policy Tests

    [Fact]
    public async Task RequireEditorOrAbove_SuperAdminRole_ReturnsSuccess()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("SuperAdmin");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireEditorOrAbove");

        // Assert
        result.Succeeded.Should().BeTrue("SuperAdmin should have access to RequireEditorOrAbove policy");
    }

    [Fact]
    public async Task RequireEditorOrAbove_AdminRole_ReturnsSuccess()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("Admin");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireEditorOrAbove");

        // Assert
        result.Succeeded.Should().BeTrue("Admin should have access to RequireEditorOrAbove policy");
    }

    [Fact]
    public async Task RequireEditorOrAbove_EditorRole_ReturnsSuccess()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("Editor");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireEditorOrAbove");

        // Assert
        result.Succeeded.Should().BeTrue("Editor should have access to RequireEditorOrAbove policy");
    }

    [Fact]
    public async Task RequireEditorOrAbove_UserRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithRole("User");

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireEditorOrAbove");

        // Assert
        result.Succeeded.Should().BeFalse("User should NOT have access to RequireEditorOrAbove policy");
    }

    [Fact]
    public async Task RequireEditorOrAbove_NoRole_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipalWithoutRole();

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireEditorOrAbove");

        // Assert
        result.Succeeded.Should().BeFalse("Anonymous user should NOT have access to RequireEditorOrAbove policy");
    }

    #endregion

    #region Role Case Sensitivity Tests

    [Theory]
    [InlineData("superadmin")]
    [InlineData("SUPERADMIN")]
    [InlineData("SuperADMIN")]
    public async Task RequireSuperAdmin_CaseVariations_ShouldFail(string roleName)
    {
        // Arrange - ASP.NET Core role claims are case-sensitive by default
        var principal = CreatePrincipalWithRole(roleName);

        // Act
        var result = await _authorizationService.AuthorizeAsync(principal, null, "RequireSuperAdmin");

        // Assert - Only exact "SuperAdmin" should work
        // Note: This documents the expected behavior - roles are case-sensitive
        result.Succeeded.Should().BeFalse($"Role '{roleName}' should not match 'SuperAdmin' (case-sensitive)");
    }

    #endregion

    #region Helper Methods

    private static ClaimsPrincipal CreatePrincipalWithRole(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, "test@example.com"),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "SessionCookie");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreatePrincipalWithoutRole()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, "anonymous@example.com")
        };

        var identity = new ClaimsIdentity(claims, "SessionCookie");
        return new ClaimsPrincipal(identity);
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
