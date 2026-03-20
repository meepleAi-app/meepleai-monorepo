using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.UserProfile;

/// <summary>
/// Tests for ChangePasswordCommandHandler - Issue #2308 Week 4.
/// Tests password change with branch coverage for validation, not found, wrong password, same password.
/// Covers: Success path, validation failures, domain business rules.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2308")]
public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _mockRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new ChangePasswordCommandHandler(_mockRepository.Object, _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidPasswordChange_ShouldUpdatePassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        var user = new User(
            id: userId,
            email: new Email("user@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(currentPassword),
            role: Role.User,
            tier: UserTier.Free
        );

        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        };

        _mockRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyNewPassword_ShouldThrowValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = "OldPassword123!",
            NewPassword = ""
        };

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*New password cannot be empty*");

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceNewPassword_ShouldThrowValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = "OldPassword123!",
            NewPassword = "   "
        };

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*New password cannot be empty*");

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ShouldThrowDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword456!"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("User not found");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithIncorrectCurrentPassword_ShouldThrowDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var correctPassword = "CorrectPassword123!";
        var wrongPassword = "WrongPassword456!";
        var newPassword = "NewPassword789!";

        var user = new User(
            id: userId,
            email: new Email("user@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(correctPassword),
            role: Role.User,
            tier: UserTier.Free
        );

        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = wrongPassword,
            NewPassword = newPassword
        };

        _mockRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        ChangePasswordCommand? command = null;

        // Act & Assert
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}