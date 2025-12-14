using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
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
/// Comprehensive tests for DeleteUserCommandHandler.
/// Tests user deletion with business rules: no self-deletion, preserve last admin.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteUserCommandHandler _handler;

    public DeleteUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new DeleteUserCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_RegularUser_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid(); // Different user
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("user@example.com")
            .Build(); // Regular user role

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EditorUser_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("editor@example.com")
            .AsEditor()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AdminUser_WhenMultipleAdmins_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var adminUser = new UserBuilder()
            .WithId(userId)
            .WithEmail("admin@example.com")
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // Multiple admins exist

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.CountAdminsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(adminUser, It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_SelfDeletion_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: userId.ToString()); // Same user

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Equal("Cannot delete your own account", exception.Message);

        // Verify user was NOT deleted
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_LastAdmin_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var lastAdminUser = new UserBuilder()
            .WithId(userId)
            .WithEmail("last-admin@example.com")
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastAdminUser);

        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Only one admin

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Equal("Cannot delete the last admin user", exception.Message);

        // Verify user was NOT deleted
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"User {userId} not found", exception.Message);

        // Verify delete was NOT called
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task Handle_AdminCountExactlyOne_PreventsDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var adminUser = new UserBuilder()
            .WithId(userId)
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Exactly 1 admin

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_AdminCountTwo_AllowsDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var adminUser = new UserBuilder()
            .WithId(userId)
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2); // 2 admins (can delete one)

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Should succeed
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(adminUser, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RegularUser_DoesNotCheckAdminCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var regularUser = new UserBuilder()
            .WithId(userId)
            .Build(); // Regular user role

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(regularUser);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - CountAdminsAsync should NOT be called for non-admin users
        _userRepositoryMock.Verify(
            r => r.CountAdminsAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(userId, cancellationToken),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(user, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}
