using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Comprehensive tests for ChangeUserRoleCommandHandler.
/// Tests role assignment and updates.
/// </summary>
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

    #region Happy Path Tests

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
            NewRole: "admin");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId.ToString(), result.Id);
        Assert.Equal("admin", result.Role);
        Assert.Equal("user@example.com", result.Email);

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
            NewRole: "editor");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("editor", result.Role);
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
            NewRole: "user");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("user", result.Role);
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
            NewRole: "user");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("user", result.Role);
    }

    #endregion

    #region Edge Cases

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
            NewRole: "admin");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains($"User {userId} not found", exception.Message);

        // Verify save was NOT called
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
            NewRole: "admin"); // Same role

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Should still save (idempotent operation)
        Assert.Equal("admin", result.Role);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region DTO Mapping Tests

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
            NewRole: "editor");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - All user details should be preserved
        Assert.Equal(userId.ToString(), result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("editor", result.Role);
        Assert.Equal(originalCreatedAt, result.CreatedAt);
    }

    #endregion

    #region Cancellation Tests

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
            NewRole: "admin");

        var cts = new CancellationTokenSource();
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

    #endregion
}
