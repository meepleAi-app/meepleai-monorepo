using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Domain tests for User aggregate root.
/// DDD-PHASE2: Tests aligned with Guid schema.
/// </summary>
public class UserDomainTests
{
    [Fact]
    public void User_Create_WithValidData_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("SecurePassword123!");
        var role = Role.User;

        // Act
        var user = new User(id, email, "Test User", passwordHash, role);

        // Assert
        Assert.Equal(id, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(role, user.Role);
        Assert.False(user.IsTwoFactorEnabled);
    }

    [Fact]
    public void User_VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser(password);

        // Act & Assert
        Assert.True(user.VerifyPassword(password));
    }

    [Fact]
    public void User_VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser("CorrectPassword123!");

        // Act & Assert
        Assert.False(user.VerifyPassword("WrongPassword!"));
    }

    [Fact]
    public void User_EnableTwoFactor_WhenNotEnabled_EnablesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.EnableTwoFactor("encrypted_secret_base64");

        // Assert
        Assert.True(user.IsTwoFactorEnabled);
        Assert.NotNull(user.TotpSecretEncrypted);
        Assert.NotNull(user.TwoFactorEnabledAt);
    }

    [Fact]
    public void User_DisableTwoFactor_WhenEnabled_DisablesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        user.EnableTwoFactor("secret");

        // Act
        user.DisableTwoFactor();

        // Assert
        Assert.False(user.IsTwoFactorEnabled);
        Assert.Null(user.TotpSecretEncrypted);
    }

    [Fact]
    public void User_AssignRole_ByAdmin_UpdatesRole()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.AssignRole(Role.Editor, Role.Admin);

        // Assert
        Assert.Equal(Role.Editor, user.Role);
    }

    [Fact]
    public void User_AssignRole_ByNonAdmin_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            user.AssignRole(Role.Admin, Role.Editor));
    }

    private static User CreateTestUser(
        string password = "TestPassword123!",
        Role? role = null)
    {
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create(password);
        var userRole = role ?? Role.User;

        return new User(id, email, "Test User", passwordHash, userRole);
    }
}
