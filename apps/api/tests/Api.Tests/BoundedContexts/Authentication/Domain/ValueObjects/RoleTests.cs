using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class RoleTests
{
    [Theory]
    [InlineData("user")]
    [InlineData("editor")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public void Parse_WithValidRole_CreatesSuccessfully(string roleValue)
    {
        // Act
        var role = Role.Parse(roleValue);

        // Assert
        role.Value.Should().Be(roleValue.ToLowerInvariant());
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
        role.Value.Should().Be(roleValue.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_WithEmptyValue_ThrowsValidationException(string invalidRole)
    {
        // Act & Assert
        var act = () => Role.Parse(invalidRole);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Role cannot be empty");
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("moderator")]
    [InlineData("invalid")]
    public void Parse_WithInvalidRole_ThrowsValidationException(string invalidRole)
    {
        // Act & Assert
        var act = () => Role.Parse(invalidRole);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Invalid role");
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
        role.IsAdmin().Should().Be(expectedIsAdmin);
        role.IsEditor().Should().Be(expectedIsEditor);
        role.IsUser().Should().Be(expectedIsUser);
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
        role.HasPermission(permissionRole).Should().Be(expected);
    }

    [Fact]
    public void StaticInstances_AreCorrectlyDefined()
    {
        // Assert
        Role.Admin.Value.Should().Be(Role.Admin.Value);
        Role.Editor.Value.Should().Be(Role.Editor.Value);
        Role.User.Value.Should().Be(Role.User.Value);
    }

    [Fact]
    public void EqualityComparison_WithSameRole_AreEqual()
    {
        // Arrange
        var role1 = Role.Parse(Role.Admin.Value);
        var role2 = Role.Parse("ADMIN");
        var role3 = Role.Admin;

        // Act & Assert
        role2.Should().Be(role1);
        role3.Should().Be(role1);
        role3.Should().Be(role2);
    }

    [Fact]
    public void EqualityComparison_WithDifferentRoles_AreNotEqual()
    {
        // Arrange
        var adminRole = Role.Admin;
        var editorRole = Role.Editor;
        var userRole = Role.User;

        // Act & Assert
        editorRole.Should().NotBe(adminRole);
        userRole.Should().NotBe(adminRole);
        userRole.Should().NotBe(editorRole);
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
        result.Should().Be(roleValue);
    }

    [Fact]
    public void ImplicitConversion_ToStringWorks()
    {
        // Arrange
        var role = Role.Admin;

        // Act
        string roleString = role;

        // Assert
        roleString.Should().Be(Role.Admin.Value);
    }

    [Fact]
    public void Parse_WithCreator_ShouldReturnCreatorRole()
    {
        var role = Role.Parse("creator");
        role.Value.Should().Be("creator");
        role.IsCreator().Should().BeTrue();
    }

    [Fact]
    public void Creator_HasPermission_ForCreatorAndUser_Only()
    {
        var creator = Role.Creator;
        creator.HasPermission(Role.Creator).Should().BeTrue();
        creator.HasPermission(Role.User).Should().BeTrue();
        creator.HasPermission(Role.Editor).Should().BeFalse();
        creator.HasPermission(Role.Admin).Should().BeFalse();
        creator.HasPermission(Role.SuperAdmin).Should().BeFalse();
    }

    [Fact]
    public void Editor_HasPermission_ForCreator()
    {
        var editor = Role.Editor;
        editor.HasPermission(Role.Creator).Should().BeTrue();
        editor.HasPermission(Role.Editor).Should().BeTrue();
        editor.HasPermission(Role.User).Should().BeTrue();
        editor.HasPermission(Role.Admin).Should().BeFalse();
        editor.HasPermission(Role.SuperAdmin).Should().BeFalse();
    }

    [Fact]
    public void Admin_HasPermission_ForCreator()
    {
        var admin = Role.Admin;
        admin.HasPermission(Role.Creator).Should().BeTrue();
        admin.HasPermission(Role.Editor).Should().BeTrue();
        admin.HasPermission(Role.User).Should().BeTrue();
        admin.HasPermission(Role.Admin).Should().BeTrue();
        admin.HasPermission(Role.SuperAdmin).Should().BeFalse();
    }

    [Fact]
    public void SuperAdmin_HasPermission_ForAllRoles()
    {
        var superAdmin = Role.SuperAdmin;
        superAdmin.HasPermission(Role.User).Should().BeTrue();
        superAdmin.HasPermission(Role.Creator).Should().BeTrue();
        superAdmin.HasPermission(Role.Editor).Should().BeTrue();
        superAdmin.HasPermission(Role.Admin).Should().BeTrue();
        superAdmin.HasPermission(Role.SuperAdmin).Should().BeTrue();
    }
}