using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Comprehensive tests for ChangeUserRoleCommandHandler.
/// Tests role assignment and updates.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ChangeUserRoleCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ChangeUserRoleCommandHandler _handler;

    public ChangeUserRoleCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new ChangeUserRoleCommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_PromoteUserToAdmin_UpdatesRoleAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("user@example.com")
            .Build(); // Regular user role

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Admin.Value);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId.ToString(), result.Id);
        Assert.Equal(Role.Admin.Value, result.Role);
        Assert.Equal("user@example.com", result.Email);

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PromoteUserToEditor_UpdatesRole()
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

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Editor.Value);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.Editor.Value, result.Role);

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DemoteAdminToUser_UpdatesRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new UserBuilder()
            .WithId(userId)
            .WithEmail("admin@example.com")
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.User.Value);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.User.Value, result.Role);

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DemoteEditorToUser_UpdatesRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var editorUser = new UserBuilder()
            .WithId(userId)
            .AsEditor()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editorUser);

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.User.Value);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.User.Value, result.Role);

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
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

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Admin.Value);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"User {userId} not found", exception.Message);

        // Verify update and save were NOT called
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameRole_StillUpdates()
    {
        // Arrange - User already has admin role
        var userId = Guid.NewGuid();
        var adminUser = new UserBuilder()
            .WithId(userId)
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Admin.Value); // Same role

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Should still save (idempotent operation)
        Assert.Equal(Role.Admin.Value, result.Role);
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_PreservesUserDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        var originalCreatedAt = user.CreatedAt;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Editor.Value);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All user details should be preserved
        Assert.Equal(userId.ToString(), result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal(Role.Editor.Value, result.Role);
        Assert.Equal(originalCreatedAt, result.CreatedAt);
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

        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Admin.Value);

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(userId, cancellationToken),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}