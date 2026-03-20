using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application;

/// <summary>
/// Application layer tests for user profile management handlers.
/// Tests handler logic with mocked dependencies.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UserProfileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public UserProfileHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }
    [Fact]
    public async Task GetUserProfile_UserExists_ReturnsProfileDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new GetUserProfileQueryHandler(_userRepositoryMock.Object);
        var query = new GetUserProfileQuery { UserId = userId };

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be(user.Email.Value);
        result.DisplayName.Should().Be(user.DisplayName);
        result.Role.Should().Be(user.Role.Value);
    }

    [Fact]
    public async Task GetUserProfile_UserNotFound_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new GetUserProfileQueryHandler(_userRepositoryMock.Object);
        var query = new GetUserProfileQuery { UserId = userId };

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
    [Fact]
    public async Task UpdateProfile_DisplayNameOnly_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new UpdateUserProfileCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateUserProfileCommand
        {
            UserId = userId,
            DisplayName = "New Display Name",
            Email = null
        };

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        user.DisplayName.Should().Be("New Display Name");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_EmailOnly_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new UpdateUserProfileCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateUserProfileCommand
        {
            UserId = userId,
            DisplayName = null,
            Email = "newemail@test.com"
        };

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        user.Email.Value.Should().Be("newemail@test.com");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_EmailInUse_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        var otherUser = CreateTestUser(Guid.NewGuid());

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        var handler = new UpdateUserProfileCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateUserProfileCommand
        {
            UserId = userId,
            DisplayName = null,
            Email = "existing@test.com"
        };

        // Act & Assert
        var act = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("Email is already in use");
    }

    [Fact]
    public async Task UpdateProfile_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new UpdateUserProfileCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateUserProfileCommand
        {
            UserId = userId,
            DisplayName = "New Name",
            Email = null
        };

        // Act & Assert
        var act = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("User not found");
    }
    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_ChangesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "OldPassword123!";
        var user = CreateTestUser(userId, currentPassword);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new ChangePasswordCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = currentPassword,
            NewPassword = "NewPassword456!"
        };

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        user.VerifyPassword("NewPassword456!").Should().BeTrue();
        user.VerifyPassword(currentPassword).Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_IncorrectCurrentPassword_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "OldPassword123!");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new ChangePasswordCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = "WrongPassword!",
            NewPassword = "NewPassword456!"
        };

        // Act & Assert
        var act = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("Current password is incorrect");
    }

    [Fact]
    public async Task ChangePassword_EmptyNewPassword_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new ChangePasswordCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = "OldPassword123!",
            NewPassword = ""
        };

        // Act & Assert
        var act = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ChangePassword_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new ChangePasswordCommandHandler(_userRepositoryMock.Object, _unitOfWorkMock.Object);
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword456!"
        };

        // Act & Assert
        var act = () =>
            handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("User not found");
    }
    private static User CreateTestUser(Guid? userId = null, string password = "TestPassword123!")
    {
        return new User(
            id: userId ?? Guid.NewGuid(),
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(password),
            role: Role.User
        );
    }
}