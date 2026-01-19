using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
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

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user2);

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

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

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
        var command = new BulkRoleChangeCommand(
            new List<Guid> { Guid.NewGuid() },
            "invalid_role",
            Guid.NewGuid()
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
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
        await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
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
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
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
        var user = CreateTestUser(userId, "user@test.com", Role.User);

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new BulkRoleChangeCommand(
            new List<Guid> { userId },
            roleName,
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.SuccessCount.Should().Be(1);
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
}
