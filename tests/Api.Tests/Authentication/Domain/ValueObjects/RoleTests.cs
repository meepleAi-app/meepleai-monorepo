using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Authentication.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class RoleTests
{
    #region Static Instance Tests

    [Fact]
    public void User_ReturnsUserRole()
    {
        // Act
        var role = Role.User;

        // Assert
        role.Value.Should().Be("user");
    }

    [Fact]
    public void Editor_ReturnsEditorRole()
    {
        // Act
        var role = Role.Editor;

        // Assert
        role.Value.Should().Be("editor");
    }

    [Fact]
    public void Admin_ReturnsAdminRole()
    {
        // Act
        var role = Role.Admin;

        // Assert
        role.Value.Should().Be("admin");
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("user")]
    [InlineData("USER")]
    [InlineData("User")]
    public void Parse_UserVariants_ReturnsUserRole(string value)
    {
        // Act
        var role = Role.Parse(value);

        // Assert
        role.Value.Should().Be("user");
    }

    [Theory]
    [InlineData("editor")]
    [InlineData("EDITOR")]
    [InlineData("Editor")]
    public void Parse_EditorVariants_ReturnsEditorRole(string value)
    {
        // Act
        var role = Role.Parse(value);

        // Assert
        role.Value.Should().Be("editor");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Admin")]
    public void Parse_AdminVariants_ReturnsAdminRole(string value)
    {
        // Act
        var role = Role.Parse(value);

        // Assert
        role.Value.Should().Be("admin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithEmptyValue_ThrowsValidationException(string? invalidValue)
    {
        // Act & Assert
        var action = () => Role.Parse(invalidValue!);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Role cannot be empty*");
    }

    [Theory]
    [InlineData("superuser")]
    [InlineData("moderator")]
    [InlineData("guest")]
    [InlineData("invalid")]
    public void Parse_WithInvalidRole_ThrowsValidationException(string invalidRole)
    {
        // Act & Assert
        var action = () => Role.Parse(invalidRole);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Invalid role*")
            .And.Message.Should().Contain("user, editor, admin");
    }

    #endregion

    #region IsAdmin/IsEditor/IsUser Tests

    [Fact]
    public void IsAdmin_ForAdminRole_ReturnsTrue()
    {
        Role.Admin.IsAdmin().Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_ForNonAdminRole_ReturnsFalse()
    {
        Role.User.IsAdmin().Should().BeFalse();
        Role.Editor.IsAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsEditor_ForEditorRole_ReturnsTrue()
    {
        Role.Editor.IsEditor().Should().BeTrue();
    }

    [Fact]
    public void IsEditor_ForNonEditorRole_ReturnsFalse()
    {
        Role.User.IsEditor().Should().BeFalse();
        Role.Admin.IsEditor().Should().BeFalse();
    }

    [Fact]
    public void IsUser_ForUserRole_ReturnsTrue()
    {
        Role.User.IsUser().Should().BeTrue();
    }

    [Fact]
    public void IsUser_ForNonUserRole_ReturnsFalse()
    {
        Role.Editor.IsUser().Should().BeFalse();
        Role.Admin.IsUser().Should().BeFalse();
    }

    #endregion

    #region HasPermission Tests

    [Fact]
    public void HasPermission_AdminToAnyRole_ReturnsTrue()
    {
        // Admin has all permissions
        Role.Admin.HasPermission(Role.Admin).Should().BeTrue();
        Role.Admin.HasPermission(Role.Editor).Should().BeTrue();
        Role.Admin.HasPermission(Role.User).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_EditorToUserOrEditor_ReturnsTrue()
    {
        // Editor has editor + user permissions
        Role.Editor.HasPermission(Role.Editor).Should().BeTrue();
        Role.Editor.HasPermission(Role.User).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_EditorToAdmin_ReturnsFalse()
    {
        // Editor does not have admin permissions
        Role.Editor.HasPermission(Role.Admin).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_UserToUser_ReturnsTrue()
    {
        // User has user permissions only
        Role.User.HasPermission(Role.User).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_UserToEditorOrAdmin_ReturnsFalse()
    {
        // User does not have editor or admin permissions
        Role.User.HasPermission(Role.Editor).Should().BeFalse();
        Role.User.HasPermission(Role.Admin).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WithNullRole_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => Role.User.HasPermission(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        Role.User.ToString().Should().Be("user");
        Role.Editor.ToString().Should().Be("editor");
        Role.Admin.ToString().Should().Be("admin");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        // Arrange
        Role role = Role.Admin;

        // Act
        string stringValue = role;

        // Assert
        stringValue.Should().Be("admin");
    }

    [Fact]
    public void ImplicitConversionToString_WithNullRole_ThrowsArgumentNullException()
    {
        // Arrange
        Role? role = null;

        // Act & Assert
        var action = () => { string _ = role!; };
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameRole_AreEqual()
    {
        // Arrange
        var role1 = Role.Parse("admin");
        var role2 = Role.Parse("ADMIN");

        // Act & Assert
        role1.Should().Be(role2);
    }

    [Fact]
    public void Equals_DifferentRoles_AreNotEqual()
    {
        // Act & Assert
        Role.User.Should().NotBe(Role.Editor);
        Role.Editor.Should().NotBe(Role.Admin);
        Role.Admin.Should().NotBe(Role.User);
    }

    [Fact]
    public void GetHashCode_SameRole_ReturnsSameHashCode()
    {
        // Arrange
        var role1 = Role.Parse("user");
        var role2 = Role.Parse("USER");

        // Act & Assert
        role1.GetHashCode().Should().Be(role2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentRoles_ReturnDifferentHashCodes()
    {
        // Arrange
        var allRoles = new[] { Role.User, Role.Editor, Role.Admin };

        // Act
        var hashCodes = allRoles.Select(r => r.GetHashCode()).ToList();

        // Assert
        hashCodes.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region Permission Hierarchy Tests

    [Fact]
    public void PermissionHierarchy_IsCorrect()
    {
        // Test complete permission hierarchy

        // Admin can do everything
        Role.Admin.HasPermission(Role.Admin).Should().BeTrue("Admin should have Admin permission");
        Role.Admin.HasPermission(Role.Editor).Should().BeTrue("Admin should have Editor permission");
        Role.Admin.HasPermission(Role.User).Should().BeTrue("Admin should have User permission");

        // Editor can do Editor and User things
        Role.Editor.HasPermission(Role.Admin).Should().BeFalse("Editor should not have Admin permission");
        Role.Editor.HasPermission(Role.Editor).Should().BeTrue("Editor should have Editor permission");
        Role.Editor.HasPermission(Role.User).Should().BeTrue("Editor should have User permission");

        // User can only do User things
        Role.User.HasPermission(Role.Admin).Should().BeFalse("User should not have Admin permission");
        Role.User.HasPermission(Role.Editor).Should().BeFalse("User should not have Editor permission");
        Role.User.HasPermission(Role.User).Should().BeTrue("User should have User permission");
    }

    #endregion
}
