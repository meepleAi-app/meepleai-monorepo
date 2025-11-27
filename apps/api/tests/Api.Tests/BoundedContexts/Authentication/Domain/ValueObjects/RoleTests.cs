using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

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

    [Fact]
    public void IsAdmin_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var role = Role.Admin;

        // Act & Assert
        Assert.True(role.IsAdmin());
        Assert.False(role.IsEditor());
        Assert.False(role.IsUser());
    }

    [Fact]
    public void IsEditor_WithEditorRole_ReturnsTrue()
    {
        // Arrange
        var role = Role.Editor;

        // Act & Assert
        Assert.False(role.IsAdmin());
        Assert.True(role.IsEditor());
        Assert.False(role.IsUser());
    }

    [Fact]
    public void IsUser_WithUserRole_ReturnsTrue()
    {
        // Arrange
        var role = Role.User;

        // Act & Assert
        Assert.False(role.IsAdmin());
        Assert.False(role.IsEditor());
        Assert.True(role.IsUser());
    }

    [Fact]
    public void HasPermission_AdminRole_HasAllPermissions()
    {
        // Arrange
        var adminRole = Role.Admin;

        // Act & Assert
        Assert.True(adminRole.HasPermission(Role.Admin));
        Assert.True(adminRole.HasPermission(Role.Editor));
        Assert.True(adminRole.HasPermission(Role.User));
    }

    [Fact]
    public void HasPermission_EditorRole_HasEditorAndUserPermissions()
    {
        // Arrange
        var editorRole = Role.Editor;

        // Act & Assert
        Assert.False(editorRole.HasPermission(Role.Admin));
        Assert.True(editorRole.HasPermission(Role.Editor));
        Assert.True(editorRole.HasPermission(Role.User));
    }

    [Fact]
    public void HasPermission_UserRole_HasUserPermissionsOnly()
    {
        // Arrange
        var userRole = Role.User;

        // Act & Assert
        Assert.False(userRole.HasPermission(Role.Admin));
        Assert.False(userRole.HasPermission(Role.Editor));
        Assert.True(userRole.HasPermission(Role.User));
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

