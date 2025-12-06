using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class RoleTests
{
    [Theory]
    [InlineData("user")]
    [InlineData("editor")]
    [InlineData("admin")]
    public void Parse_WithValidRole_CreatesSuccessfully(string roleValue)
    {
        // Act
        var role = Role.Parse(roleValue);

        // Assert
        Assert.Equal(roleValue.ToLowerInvariant(), role.Value);
    }

    [Theory]
    [InlineData("USER")]
    [InlineData("Editor")]
    [InlineData("ADMIN")]
    [InlineData("AdMiN")]
    public void Parse_WithDifferentCasing_NormalizesToLowercase(string roleValue)
    {
        // Act
        var role = Role.Parse(roleValue);

        // Assert
        Assert.Equal(roleValue.ToLowerInvariant(), role.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_WithEmptyValue_ThrowsValidationException(string invalidRole)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => Role.Parse(invalidRole));
        Assert.Contains("Role cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData("guest")]
    [InlineData("moderator")]
    [InlineData("invalid")]
    public void Parse_WithInvalidRole_ThrowsValidationException(string invalidRole)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => Role.Parse(invalidRole));
        Assert.Contains("Invalid role", exception.Message);
        Assert.Contains("Valid roles are: user, editor, admin", exception.Message);
    }

    [Theory]
    [InlineData("admin", true, false, false)]
    [InlineData("editor", false, true, false)]
    [InlineData("user", false, false, true)]
    public void IsRoleType_ReturnsCorrectValue(string roleValue, bool expectedIsAdmin, bool expectedIsEditor, bool expectedIsUser)
    {
        // Arrange
        var role = Role.Parse(roleValue);

        // Act & Assert
        Assert.Equal(expectedIsAdmin, role.IsAdmin());
        Assert.Equal(expectedIsEditor, role.IsEditor());
        Assert.Equal(expectedIsUser, role.IsUser());
    }

    [Theory]
    [InlineData("admin", "admin", true)]     // Admin has Admin permission
    [InlineData("admin", "editor", true)]    // Admin has Editor permission
    [InlineData("admin", "user", true)]      // Admin has User permission
    [InlineData("editor", "admin", false)]   // Editor doesn't have Admin permission
    [InlineData("editor", "editor", true)]   // Editor has Editor permission
    [InlineData("editor", "user", true)]     // Editor has User permission
    [InlineData("user", "admin", false)]     // User doesn't have Admin permission
    [InlineData("user", "editor", false)]    // User doesn't have Editor permission
    [InlineData("user", "user", true)]       // User has User permission
    public void HasPermission_ReturnsCorrectPermissionHierarchy(string roleValue, string requiredPermission, bool expected)
    {
        // Arrange
        var role = Role.Parse(roleValue);
        var permissionRole = Role.Parse(requiredPermission);

        // Act & Assert
        Assert.Equal(expected, role.HasPermission(permissionRole));
    }

    [Fact]
    public void StaticInstances_AreCorrectlyDefined()
    {
        // Assert
        Assert.Equal(Role.Admin.Value, Role.Admin.Value);
        Assert.Equal(Role.Editor.Value, Role.Editor.Value);
        Assert.Equal(Role.User.Value, Role.User.Value);
    }

    [Fact]
    public void EqualityComparison_WithSameRole_AreEqual()
    {
        // Arrange
        var role1 = Role.Parse(Role.Admin.Value);
        var role2 = Role.Parse("ADMIN");
        var role3 = Role.Admin;

        // Act & Assert
        Assert.Equal(role1, role2);
        Assert.Equal(role1, role3);
        Assert.Equal(role2, role3);
    }

    [Fact]
    public void EqualityComparison_WithDifferentRoles_AreNotEqual()
    {
        // Arrange
        var adminRole = Role.Admin;
        var editorRole = Role.Editor;
        var userRole = Role.User;

        // Act & Assert
        Assert.NotEqual(adminRole, editorRole);
        Assert.NotEqual(adminRole, userRole);
        Assert.NotEqual(editorRole, userRole);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("editor")]
    [InlineData("admin")]
    public void ToString_ReturnsCorrectValue(string roleValue)
    {
        // Arrange
        var role = Role.Parse(roleValue);

        // Act
        var result = role.ToString();

        // Assert
        Assert.Equal(roleValue, result);
    }

    [Fact]
    public void ImplicitConversion_ToStringWorks()
    {
        // Arrange
        var role = Role.Admin;

        // Act
        string roleString = role;

        // Assert
        Assert.Equal(Role.Admin.Value, roleString);
    }
}