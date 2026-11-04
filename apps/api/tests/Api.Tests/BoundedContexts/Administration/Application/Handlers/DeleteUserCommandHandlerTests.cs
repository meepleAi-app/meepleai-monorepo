using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

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
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;

        exception.Message.Should().Be("Cannot delete your own account");

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
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;

        exception.Message.Should().Be("Cannot delete the last admin user");

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
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;

        exception.Message.Should().Contain($"User {userId} not found");

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
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<DomainException>();
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

    // === VALIDATION FAILURE TESTS (Week 10-11: Validation branch coverage) ===

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyUserId_ThrowsValidationException(string emptyUserId)
    {
        // Arrange
        var command = new DeleteUserCommand(
            UserId: emptyUserId,
            RequestingUserId: Guid.NewGuid().ToString());

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyRequestingUserId_ThrowsValidationException(string emptyRequestingUserId)
    {
        // Arrange
        var command = new DeleteUserCommand(
            UserId: Guid.NewGuid().ToString(),
            RequestingUserId: emptyRequestingUserId);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidUserIdGuid_ThrowsValidationException()
    {
        // Arrange
        var command = new DeleteUserCommand(
            UserId: "not-a-guid",
            RequestingUserId: Guid.NewGuid().ToString());

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidRequestingUserIdGuid_ThrowsValidationException()
    {
        // Arrange
        var command = new DeleteUserCommand(
            UserId: Guid.NewGuid().ToString(),
            RequestingUserId: "not-a-guid");

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
        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: Guid.NewGuid().ToString());

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<DomainException>();

        _userRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    #region #3873 — PROTEGGE: the last-privileged-account guard must include superadmin

    /// <summary>
    /// #3873 DoD 5 — superadmin as the subject. Trigger and counter disagree: the trigger at
    /// DeleteUserCommandHandler.cs:49 is <c>Role.IsAdmin()</c> (exact match on "admin"), while
    /// <c>CountAdminsAsync</c> counts <c>admin OR superadmin</c>. A lone superadmin is therefore
    /// counted as the last privileged account by the counter but never reaches it.
    /// </summary>
    [Theory]
    [InlineData("admin")]       // guarded today
    [InlineData("superadmin")]  // #3873: NOT guarded today — the trigger is an exact match
    public async Task Handle_LastPrivilegedAccount_ThrowsDomainException(string roleValue)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithEmail($"last-{roleValue}@example.com")
            .WithRole(AuthRole.Parse(roleValue))
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // CountAdminsAsync counts admin OR superadmin: 1 means this account IS the last one.
        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<DomainException>();

        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The good direction, which must stay green: with a second privileged account left the guard
    /// must not fire. Pins the boundary (<c>adminCount &lt;= 1</c>) so widening the trigger to
    /// superadmin does not become an over-block.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task Handle_PrivilegedAccount_WithAnotherPrivilegedAccountLeft_DeletesSuccessfully(string roleValue)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithRole(AuthRole.Parse(roleValue))
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var command = new DeleteUserCommand(
            UserId: userId.ToString(),
            RequestingUserId: requestingUserId.ToString());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(user, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Anti-widening net: the guard protects privileged accounts only. An unprivileged account is
    /// deletable even when a single admin remains in the system.
    /// </summary>
    [Theory]
    [InlineData("user")]
    [InlineData("editor")]
    [InlineData("creator")]
    public async Task Handle_UnprivilegedAccount_IsNotProtectedByTheLastAdminGuard(string roleValue)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithRole(AuthRole.Parse(roleValue))
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Even with a single admin left in the system, this account is not the one being protected.
        _userRepositoryMock
            .Setup(r => r.CountAdminsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

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

    #endregion
}
