using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Comprehensive tests for AdminDisable2FACommandHandler.
/// Tests admin override to disable 2FA for locked-out users.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AdminDisable2FACommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<AdminDisable2FACommandHandler>> _loggerMock;
    private readonly AdminDisable2FACommandHandler _handler;

    public AdminDisable2FACommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<AdminDisable2FACommandHandler>>();
        _handler = new AdminDisable2FACommandHandler(
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }
    [Fact]
    public async Task Handle_ValidAdminAndTarget_DisablesSuccessfully()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminUserId)
            .WithEmail("admin@example.com")
            .AsAdmin()
            .Build();

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .WithEmail("user@example.com")
            .With2FA()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: targetUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.False(targetUser.IsTwoFactorEnabled); // 2FA should be disabled
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(targetUser, It.IsAny<CancellationToken>()),
            Times.Once,
            "Repository UpdateAsync must be called to persist 2FA state changes");
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_AdminUserNotFound_ReturnsError()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: targetUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Admin user not found", result.ErrorMessage);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NonAdminUser_ReturnsUnauthorized()
    {
        // Arrange
        var nonAdminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var nonAdminUser = new UserBuilder()
            .WithId(nonAdminUserId)
            .WithEmail("user@example.com")
            .Build(); // Regular user role

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(nonAdminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonAdminUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: nonAdminUserId,
            TargetUserId: targetUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EditorUser_ReturnsUnauthorized()
    {
        // Arrange
        var editorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var editorUser = new UserBuilder()
            .WithId(editorUserId)
            .WithEmail("editor@example.com")
            .AsEditor()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(editorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editorUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: editorUserId,
            TargetUserId: targetUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.ErrorMessage);
    }
    [Fact]
    public async Task Handle_TargetUserNotFound_ReturnsError()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminUserId)
            .AsAdmin()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: targetUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Target user not found", result.ErrorMessage);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Target2FANotEnabled_ReturnsError()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminUserId)
            .AsAdmin()
            .Build();

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .WithEmail("user@example.com")
            .Build(); // 2FA not enabled

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: targetUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not enabled", result.ErrorMessage);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AdminDisablesOwnAccount_Succeeds()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminUserId)
            .WithEmail("admin@example.com")
            .AsAdmin()
            .With2FA()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: adminUserId); // Same user

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Admin can disable their own 2FA via this endpoint
        Assert.True(result.Success);
        Assert.False(adminUser.IsTwoFactorEnabled);
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(adminUser, It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_Success_RaisesTwoFactorDisabledEvent()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminUserId)
            .AsAdmin()
            .Build();

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .With2FA()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: targetUserId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Check that domain event was raised
        var domainEvents = targetUser.DomainEvents;
        var twoFactorDisabledEvents = domainEvents.OfType<Api.BoundedContexts.Authentication.Domain.Events.TwoFactorDisabledEvent>().ToList();
        Assert.Single(twoFactorDisabledEvents);
        var twoFactorEvent = twoFactorDisabledEvents[0];
        Assert.NotNull(twoFactorEvent);
        Assert.Equal(targetUserId, twoFactorEvent.UserId);
        Assert.True(twoFactorEvent.WasAdminOverride); // Should be marked as admin override
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminUserId)
            .AsAdmin()
            .Build();

        var targetUser = new UserBuilder()
            .WithId(targetUserId)
            .With2FA()
            .Build();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var command = new AdminDisable2FACommand(
            AdminUserId: adminUserId,
            TargetUserId: targetUserId);

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(adminUserId, cancellationToken),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(targetUserId, cancellationToken),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(targetUser, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}