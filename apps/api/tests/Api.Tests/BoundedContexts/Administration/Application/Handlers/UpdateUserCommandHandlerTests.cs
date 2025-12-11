using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for UpdateUserCommandHandler.
/// Tests user updates (email, display name, role).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new UpdateUserCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidEmailUpdate_UpdatesEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("old@example.com"),
            "Test User",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            "new@example.com",
            null,
            null
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new@example.com", result.Email);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidDisplayNameUpdate_UpdatesDisplayName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("user@example.com"),
            "Old Name",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            null,
            "New Name",
            null
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.DisplayName);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidRoleUpdate_UpdatesRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("user@example.com"),
            "Test User",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            null,
            null,
            Role.Admin.Value
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("admin", result.Role); // Role values are lowercase
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserCommand(
            userId.ToString(),
            "new@example.com",
            null,
            null
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found", exception.Message);
        Assert.Contains(userId.ToString(), exception.Message);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("user@example.com"),
            "Test User",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var otherUser = new User(
            otherUserId,
            new Email("other@example.com"),
            "Other User",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            "other@example.com", // Email already used by otherUser
            null,
            null
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("already in use", exception.Message);
        Assert.Contains("other@example.com", exception.Message);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAllFieldsUpdated_UpdatesAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("old@example.com"),
            "Old Name",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            "new@example.com",
            "New Name",
            Role.Editor.Value
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new@example.com", result.Email);
        Assert.Equal("New Name", result.DisplayName);
        Assert.Equal("editor", result.Role); // Role values are lowercase
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TrimsDisplayName_BeforeSaving()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("user@example.com"),
            "Old Name",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            null,
            "  New Name  ", // Whitespace to be trimmed
            null
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.DisplayName); // No whitespace
    }

    [Fact]
    public async Task Handle_WithSameEmail_SkipsEmailUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            userId,
            new Email("same@example.com"),
            "Test User",
            PasswordHash.Create("Password123!"),
            Role.User
        );

        var command = new UpdateUserCommand(
            userId.ToString(),
            "same@example.com", // Same email as current
            null,
            null
        );

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("same@example.com", result.Email);
        // Should not check for email uniqueness if email is the same
        _mockUserRepository.Verify(
            r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
