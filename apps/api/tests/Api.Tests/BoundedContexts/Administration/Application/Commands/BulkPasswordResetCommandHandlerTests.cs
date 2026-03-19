using Api.BoundedContexts.Administration.Application.Commands;
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

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]

public class BulkPasswordResetCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkPasswordResetCommandHandler>> _mockLogger;
    private readonly BulkPasswordResetCommandHandler _handler;

    public BulkPasswordResetCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkPasswordResetCommandHandler>>();
        _handler = new BulkPasswordResetCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidUsers_ShouldResetPasswordsSuccessfully()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var newPassword = "NewSecurePassword123!";

        var user1 = CreateTestUser(userId1, "user1@test.com");
        var user2 = CreateTestUser(userId2, "user2@test.com");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user2);

        var command = new BulkPasswordResetCommand(
            new List<Guid> { userId1, userId2 },
            newPassword,
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
        var newPassword = "NewSecurePassword123!";

        var user1 = CreateTestUser(userId1, "user1@test.com");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user1);
        _mockUserRepository.Setup(r => r.GetByIdAsync(userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new BulkPasswordResetCommand(
            new List<Guid> { userId1, userId2 },
            newPassword,
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

        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyUserIdsList_ShouldThrowDomainException()
    {
        // Arrange
        var command = new BulkPasswordResetCommand(
            new List<Guid>(),
            "NewPassword123!",
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
        var command = new BulkPasswordResetCommand(
            userIds,
            "NewPassword123!",
            Guid.NewGuid()
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.Should().Contain("maximum limit of 1000");
    }

    [Fact]
    public async Task Handle_WithShortPassword_ShouldThrowDomainException()
    {
        // Arrange
        var command = new BulkPasswordResetCommand(
            new List<Guid> { Guid.NewGuid() },
            "short",
            Guid.NewGuid()
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.Handle(command, CancellationToken.None));
        exception.Message.Should().Contain("at least 8 characters");
    }

    [Fact]
    public async Task Handle_WithDuplicateUserIds_ShouldProcessDistinctUsers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var user = CreateTestUser(userId, "user@test.com");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new BulkPasswordResetCommand(
            new List<Guid> { userId, userId, userId },
            "NewPassword123!",
            requesterId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalRequested.Should().Be(3);
        result.SuccessCount.Should().Be(1);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static User CreateTestUser(Guid id, string email)
    {
        return new User(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("OldPassword123!"),
            role: Role.User
        );
    }
}
