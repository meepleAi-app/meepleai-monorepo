using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]

public class BulkRoleChangeCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkRoleChangeCommandHandler>> _mockLogger;
    private readonly BulkRoleChangeCommandHandler _handler;

    public BulkRoleChangeCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkRoleChangeCommandHandler>>();
        _handler = new BulkRoleChangeCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidUsers_ShouldChangeRolesSuccessfully()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var user1 = CreateTestUser(userId1, "user1@test.com", Role.User);
        var user2 = CreateTestUser(userId2, "user2@test.com", Role.User);

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuperAdminUser(requesterId));
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user2);
        _mockUserRepository.Setup(r => r.CountByRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var command = new BulkRoleChangeCommand(
            new List<Guid> { userId1, userId2 },
            "admin",
            requesterId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();

        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldReturnPartialSuccess()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var user1 = CreateTestUser(userId1, "user1@test.com", Role.User);

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuperAdminUser(requesterId));
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockUserRepository.Setup(r => r.CountByRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var command = new BulkRoleChangeCommand(
            new List<Guid> { userId1, userId2 },
            "admin",
            requesterId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(2);
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.Should().Contain($"User {userId2} not found");
    }

    [Fact]
    public async Task Handle_WithInvalidRole_ShouldThrowDomainException()
    {
        // Arrange
        var requesterId = Guid.NewGuid();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuperAdminUser(requesterId));

        var command = new BulkRoleChangeCommand(
            new List<Guid> { Guid.NewGuid() },
            "invalid_role",
            requesterId
        );

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("Invalid role");
    }

    [Fact]
    public async Task Handle_WithEmptyUserIdsList_ShouldThrowDomainException()
    {
        // Arrange
        var command = new BulkRoleChangeCommand(
            new List<Guid>(),
            "admin",
            Guid.NewGuid()
        );

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_WithTooManyUsers_ShouldThrowDomainException()
    {
        // Arrange
        var userIds = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToList();
        var command = new BulkRoleChangeCommand(
            userIds,
            "admin",
            Guid.NewGuid()
        );

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("maximum limit of 1000");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user")]
    [InlineData("editor")]
    public async Task Handle_WithValidRoles_ShouldSucceed(string roleName)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var user = CreateTestUser(userId, "user@test.com", Role.User);

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuperAdminUser(requesterId));
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.CountByRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var command = new BulkRoleChangeCommand(
            new List<Guid> { userId },
            roleName,
            requesterId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenInfrastructureExceptionOccurs_ShouldNotLeakExceptionMessage()
    {
        // Arrange — requester loads fine; per-user GetByIdAsync throws sensitive infrastructure exception
        const string sensitiveMessage = "EF Core transaction failed: server=prod-db;uid=sa;pwd=secret123";
        var requesterId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Requester lookup succeeds
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuperAdminUser(requesterId));

        // Per-user lookup throws with sensitive infrastructure details
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(sensitiveMessage));

        var command = new BulkRoleChangeCommand(
            new List<Guid> { userId },
            "admin",
            requesterId
        );

        // Act — the per-item catch handles the exception; no DomainException is thrown
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — the per-item error message must not leak infrastructure details
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().NotContain(sensitiveMessage,
            because: "infrastructure exception details must never be forwarded to the client");
        result.Errors[0].Should().NotContain("pwd=secret123",
            because: "credentials must never leak through error messages");
    }

    private static User CreateTestUser(Guid id, string email, Role role)
    {
        return new User(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: role
        );
    }

    private static User CreateSuperAdminUser(Guid id)
    {
        return new User(
            id: id,
            email: new Email("superadmin@test.com"),
            displayName: "Super Admin",
            passwordHash: PasswordHash.Create("SuperAdminPassword123!"),
            role: Role.SuperAdmin
        );
    }
}
