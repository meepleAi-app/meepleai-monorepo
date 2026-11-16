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

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Comprehensive tests for ResetUserPasswordCommandHandler.
/// Tests administrative password reset functionality.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ResetUserPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ResetUserPasswordCommandHandler _handler;

    public ResetUserPasswordCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new ResetUserPasswordCommandHandler(
            _userRepositoryMock.Object,
            _sessionRepositoryMock.Object,
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
            .WithPassword("OldUnusualPwd123!")
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewUnusualPwd456!",
            AdminRole: "superadmin");

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
            NewPassword: "ResetPassword789!",
            AdminRole: "superadmin");

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
            NewPassword: strongPassword,
            AdminRole: "superadmin");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_NonExistentUser_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewUnusualPwd123!",
            AdminRole: "superadmin");

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>()).Which;

        // #4907: 404, not a 400 domain_error.
        exception.ResourceId.Should().Be(userId.ToString());

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
            NewPassword: "AdminPass123!",
            AdminRole: "superadmin");

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
            NewPassword: "NewUnusualPwd123!",
            AdminRole: "superadmin");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - All user properties except password should be unchanged
        user.Id.Should().Be(userId);
        user.Email.Value.Should().Be(originalEmail);
        user.DisplayName.Should().Be(originalDisplayName);
        user.Role.Value.Should().Be(originalRole);
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
            NewPassword: "NewUnusualPwd123!",
            AdminRole: "superadmin");

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
            NewPassword: "NewUnusualPwd123!",
            AdminRole: "superadmin");

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

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
            NewPassword: invalidPassword,
            AdminRole: "superadmin");

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidUserIdGuid_ThrowsValidationException()
    {
        // Arrange
        var command = new ResetUserPasswordCommand(
            UserId: "not-a-guid",
            NewPassword: "NewUnusualPwd123!",
            AdminRole: "superadmin");

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _userRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ResetUserPasswordCommand(
            UserId: userId.ToString(),
            NewPassword: "NewUnusualPwd123!",
            AdminRole: "superadmin");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>();

        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionRepositoryMock.Verify(r => r.RevokeAllUserSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// #4907: un reset consegna l'account, quindi chi agisce deve avere un ruolo sopra quello del bersaglio. Prima del
    /// #4907 il reset non salvava, e questa regola non serviva; ora, senza, un admin si prende un superadmin.
    /// </summary>
    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("admin", "superadmin")]
    [InlineData("superadmin", "superadmin")]
    [InlineData("editor", "admin")]
    [InlineData("user", "user")] // il default del comando
    [InlineData("", "user")]
    [InlineData("not-a-role", "user")]
    public async Task Handle_TargetNotBelowTheActingRole_IsForbidden_AndSavesNothing(string adminRole, string targetRole)
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(userId).WithRole(Api.SharedKernel.Domain.ValueObjects.Role.Parse(targetRole)).Build());

        var act = () => _handler.Handle(
            new ResetUserPasswordCommand(userId.ToString(), "NewUnusualPwd123!", adminRole),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ForbiddenException>();
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _sessionRepositoryMock.Verify(r => r.RevokeAllUserSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "a refused reset must not log the target out (#5040)");
    }

    [Theory]
    [InlineData("admin", "user")]
    [InlineData("admin", "editor")]
    [InlineData("superadmin", "admin")]
    [InlineData("Admin", "user")] // il ruolo della sessione può arrivare con la maiuscola
    public async Task Handle_TargetBelowTheActingRole_IsSaved(string adminRole, string targetRole)
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(userId).WithRole(Api.SharedKernel.Domain.ValueObjects.Role.Parse(targetRole)).Build());

        await _handler.Handle(
            new ResetUserPasswordCommand(userId.ToString(), "NewUnusualPwd123!", adminRole),
            TestContext.Current.CancellationToken);

        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// #5040: il reset dell'admin chiude le sessioni dell'utente, e prima di salvare la password. Se il salvataggio
    /// fallisce, l'utente resta fuori con la vecchia password; mai dentro con sessioni che il reset doveva chiudere.
    /// </summary>
    [Fact]
    public async Task Handle_RevokesTheUsersSessions_BeforeSavingThePassword()
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(userId).Build());

        var calls = new List<string>();
        _sessionRepositoryMock
            .Setup(r => r.RevokeAllUserSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("revoke"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .ReturnsAsync(1);

        await _handler.Handle(
            new ResetUserPasswordCommand(userId.ToString(), "NewUnusualPwd123!", "admin"),
            TestContext.Current.CancellationToken);

        _sessionRepositoryMock.Verify(r => r.RevokeAllUserSessionsAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        calls.Should().Equal("revoke", "save");
    }
}
