using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for UnsuspendUserCommandHandler.
/// Issue #2886: Verify audit logging and user unsuspension.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class UnsuspendUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UnsuspendUserCommandHandler _handler;

    public UnsuspendUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new UnsuspendUserCommandHandler(
            _mockUserRepository.Object,
            _mockAuditLogRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UnsuspendsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new UnsuspendUserCommand(userId.ToString(), requesterId);

        var user = new User(
            userId,
            new Email("user@example.com"),
            "Test User",
            PasswordHash.Create("hashedPassword123"),
            Role.User
        );
        user.Suspend("Original reason");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuspended);
        Assert.Null(result.SuspendReason);
        _mockUserRepository.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new UnsuspendUserCommand(userId.ToString(), requesterId);

        var user = new User(
            userId,
            new Email("user@example.com"),
            "Test User",
            PasswordHash.Create("hashedPassword123"),
            Role.User
        );
        user.Suspend("Original reason");

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockAuditLogRepository.Verify(
            r => r.AddAsync(
                It.Is<AuditLog>(a =>
                    a.UserId == requesterId &&
                    a.Action == "user_unsuspended" &&
                    a.Resource == "User" &&
                    a.Result == "success" &&
                    a.ResourceId == userId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new UnsuspendUserCommand(userId.ToString(), requesterId);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNotSuspendedUser_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var command = new UnsuspendUserCommand(userId.ToString(), requesterId);

        var user = new User(
            userId,
            new Email("user@example.com"),
            "Test User",
            PasswordHash.Create("hashedPassword123"),
            Role.User
        );
        // User is not suspended

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
