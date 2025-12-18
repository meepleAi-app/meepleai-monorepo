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
/// Tests for CreateUserCommandHandler.
/// Tests admin user creation with role assignment.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new CreateUserCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        // Arrange
        var command = new CreateUserCommand(
            "newuser@example.com",
            "SecurePassword123!",
            "Test User",
            Role.User.Value
        );

        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("newuser@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal(Role.User.Value, result.Role);
        _mockUserRepository.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAdminRole_CreatesAdminUser()
    {
        // Arrange
        var command = new CreateUserCommand(
            "admin@example.com",
            "SecurePassword123!",
            "Admin User",
            Role.Admin.Value
        );

        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Role.Admin.Value, result.Role);
        _mockUserRepository.Verify(
            r => r.AddAsync(It.Is<User>(u => u.Role.Value == Role.Admin.Value), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new CreateUserCommand(
            "existing@example.com",
            "SecurePassword123!",
            "Test User",
            Role.User.Value
        );

        var existingUser = new User(
            Guid.NewGuid(),
            new Email("existing@example.com"),
            "Existing User",
            PasswordHash.Create("OldPassword123!"),
            Role.User
        );

        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("existing@example.com", exception.Message);
        Assert.Contains("already exists", exception.Message);

        _mockUserRepository.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEditorRole_CreatesEditorUser()
    {
        // Arrange
        var command = new CreateUserCommand(
            "editor@example.com",
            "SecurePassword123!",
            "Editor User",
            Role.Editor.Value
        );

        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.Editor.Value, result.Role);
        Assert.Null(result.LastSeenAt); // New users have no sessions yet
    }

    [Fact]
    public async Task Handle_TrimsDisplayName_BeforeSaving()
    {
        // Arrange
        var command = new CreateUserCommand(
            "user@example.com",
            "SecurePassword123!",
            "  Test User  ", // Whitespace to be trimmed
            Role.User.Value
        );

        User? capturedUser = null;
        _mockUserRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockUserRepository
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => capturedUser = user)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedUser);
        Assert.Equal("Test User", capturedUser.DisplayName); // No whitespace
        Assert.Equal("Test User", result.DisplayName);
    }
}
