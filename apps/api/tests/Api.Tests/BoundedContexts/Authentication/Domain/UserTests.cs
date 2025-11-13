using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Domain tests for User aggregate.
/// Tests business rules and validation logic.
/// </summary>
public class UserTests
{
    [Fact]
    public void UpdateDisplayName_ValidName_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var newDisplayName = "NewName";

        // Act
        user.UpdateDisplayName(newDisplayName);

        // Assert
        Assert.Equal(newDisplayName, user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_EmptyName_ThrowsValidationException(string? invalidName)
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() => user.UpdateDisplayName(invalidName!));
    }

    [Fact]
    public void UpdateDisplayName_SameName_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        var originalName = user.DisplayName;

        // Act
        user.UpdateDisplayName(originalName);

        // Assert
        Assert.Equal(originalName, user.DisplayName);
    }

    [Fact]
    public void UpdateEmail_ValidEmail_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var newEmail = new Email("newemail@test.com");

        // Act
        user.UpdateEmail(newEmail);

        // Assert
        Assert.Equal(newEmail, user.Email);
    }

    [Fact]
    public void UpdateEmail_SameEmail_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        var originalEmail = user.Email;

        // Act
        user.UpdateEmail(originalEmail);

        // Assert
        Assert.Equal(originalEmail, user.Email);
    }

    [Fact]
    public void ChangePassword_ValidCurrentPassword_ChangesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var currentPassword = "OldPassword123!";
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act
        user.ChangePassword(currentPassword, newPasswordHash);

        // Assert
        Assert.True(user.VerifyPassword("NewPassword456!"));
        Assert.False(user.VerifyPassword(currentPassword));
    }

    [Fact]
    public void ChangePassword_IncorrectCurrentPassword_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var wrongPassword = "WrongPassword123!";
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.ChangePassword(wrongPassword, newPasswordHash));
        Assert.Contains("Current password is incorrect", exception.Message);
    }

    private static User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("OldPassword123!"),
            role: Role.User
        );
    }
}
