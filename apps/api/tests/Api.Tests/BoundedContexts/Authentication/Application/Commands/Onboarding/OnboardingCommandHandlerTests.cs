using Api.BoundedContexts.Authentication.Application.Commands.Onboarding;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Onboarding;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class OnboardingCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private static User CreateTestUser(Guid? userId = null)
    {
        return new User(
            userId ?? Guid.NewGuid(),
            new Email("test@example.com"),
            "Test User",
            PasswordHash.Create("TestPassword123!"),
            Role.User);
    }

    #region MarkOnboardingWizardSeen

    [Fact]
    public async Task MarkOnboardingWizardSeen_CallsDomainMethod_AndPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new MarkOnboardingWizardSeenCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object);

        var command = new MarkOnboardingWizardSeenCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(user.OnboardingWizardSeenAt);
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkOnboardingWizardSeen_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new MarkOnboardingWizardSeenCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object);

        var command = new MarkOnboardingWizardSeenCommand(userId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task MarkOnboardingWizardSeen_AlreadySeen_StillPersists()
    {
        // Arrange — mark it once so it has a timestamp
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        user.MarkOnboardingWizardSeen();
        var originalTimestamp = user.OnboardingWizardSeenAt;

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new MarkOnboardingWizardSeenCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object);

        var command = new MarkOnboardingWizardSeenCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — idempotent: timestamp unchanged
        Assert.Equal(originalTimestamp, user.OnboardingWizardSeenAt);
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DismissOnboarding

    [Fact]
    public async Task DismissOnboarding_CallsDomainMethod_AndPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new DismissOnboardingCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object);

        var command = new DismissOnboardingCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(user.OnboardingDismissedAt);
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DismissOnboarding_UserNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new DismissOnboardingCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object);

        var command = new DismissOnboardingCommand(userId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DismissOnboarding_AlreadyDismissed_StillPersists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        user.DismissOnboarding();
        var originalTimestamp = user.OnboardingDismissedAt;

        _userRepoMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new DismissOnboardingCommandHandler(
            _userRepoMock.Object, _unitOfWorkMock.Object);

        var command = new DismissOnboardingCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — idempotent: timestamp unchanged
        Assert.Equal(originalTimestamp, user.OnboardingDismissedAt);
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
