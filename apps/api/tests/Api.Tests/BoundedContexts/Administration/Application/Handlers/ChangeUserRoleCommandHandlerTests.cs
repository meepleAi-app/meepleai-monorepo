using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
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
        result.Should().NotBeNull();
        result.Id.Should().Be(userId.ToString());
        result.Role.Should().Be(Role.Admin.Value);
        result.Email.Should().Be("user@example.com");

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
        result.Role.Should().Be(Role.Editor.Value);

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
        result.Role.Should().Be(Role.User.Value);

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
        result.Role.Should().Be(Role.User.Value);

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
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;

        exception.Message.Should().Contain($"User {userId} not found");

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
        result.Role.Should().Be(Role.Admin.Value);
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
        result.Id.Should().Be(userId.ToString());
        result.Email.Should().Be("test@example.com");
        result.DisplayName.Should().Be("Test User");
        result.Role.Should().Be(Role.Editor.Value);
        result.CreatedAt.Should().Be(originalCreatedAt);
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

        using var cts = new CancellationTokenSource();
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

    // === VALIDATION FAILURE TESTS (Week 10-11: Validation branch coverage) ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyUserId_ThrowsValidationException(string emptyUserId)
    {
        // Arrange
        var command = new ChangeUserRoleCommand(
            UserId: emptyUserId,
            NewRole: Role.Admin.Value);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("InvalidRole")]
    public async Task Handle_InvalidOrEmptyRole_ThrowsValidationException(string newRole)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: newRole);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidGuidFormat_ThrowsValidationException()
    {
        // Arrange
        var command = new ChangeUserRoleCommand(
            UserId: "not-a-guid",
            NewRole: Role.Admin.Value);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangeUserRoleCommand(
            UserId: userId.ToString(),
            NewRole: Role.Admin.Value);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<DomainException>();

        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
