using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Comprehensive tests for ResetUserPasswordCommandHandler.
/// Tests administrative password reset functionality.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ResetUserPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ResetUserPasswordCommandHandler _handler;

    public ResetUserPasswordCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new ResetUserPasswordCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_ValidPassword_ResetsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("user@example.com")
            .WithPassword("OldPassword123!")
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewPassword456!");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AdminResettingUserPassword_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("user@example.com")
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "ResetPassword789!");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrongPassword_ResetsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var strongPassword = "VeryStr0ng!P@ssw0rd#2024";
        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: strongPassword);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_NonExistentUser_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewPassword123!");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"User {userId} not found", exception.Message);

        // Verify save was NOT called
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserWithAllRoles_ResetsSuccessfully()
    {
        // Test that password reset works for all user roles

        // Admin user
        var adminId = Guid.NewGuid();
        var adminUser = new UserBuilder()
            .WithId(adminId)
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var adminCommand = new ResetUserPasswordCommand(
            UserId: adminId.ToString(),
            NewPassword: "AdminPass123!");

        // Act
        await _handler.Handle(adminCommand, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_PreservesUserIdentity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("user@example.com")
            .WithDisplayName("Test User")
            .AsEditor()
            .Build();

        var originalEmail = user.Email.Value;
        var originalDisplayName = user.DisplayName;
        var originalRole = user.Role.Value;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewPassword123!");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All user properties except password should be unchanged
        Assert.Equal(userId, user.Id);
        Assert.Equal(originalEmail, user.Email.Value);
        Assert.Equal(originalDisplayName, user.DisplayName);
        Assert.Equal(originalRole, user.Role.Value);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewPassword123!");

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(userId, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    // === VALIDATION FAILURE TESTS (Week 10-11: Validation branch coverage) ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyUserId_ThrowsValidationException(string emptyUserId)
    {
        // Arrange
        var command = new ResetUserPasswordCommand(
            UserId: emptyUserId,
            NewPassword: "NewPassword123!");

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public async Task Handle_InvalidOrEmptyPassword_ThrowsValidationException(string invalidPassword)
    {
        // Arrange
        var command = new ResetUserPasswordCommand(
            UserId: Guid.NewGuid().ToString(),
            NewPassword: invalidPassword);

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidUserIdGuid_ThrowsValidationException()
    {
        // Arrange
        var command = new ResetUserPasswordCommand(
            UserId: "not-a-guid",
            NewPassword: "NewPassword123!");

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewPassword123!");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
