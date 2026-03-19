using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class UpdateSlackPreferencesCommandHandlerTests
{
    private readonly Mock<INotificationPreferencesRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateSlackPreferencesCommandHandler>> _loggerMock;
    private readonly UpdateSlackPreferencesCommandHandler _handler;

    public UpdateSlackPreferencesCommandHandlerTests()
    {
        _repositoryMock = new Mock<INotificationPreferencesRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateSlackPreferencesCommandHandler>>();
        _handler = new UpdateSlackPreferencesCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPreferences_UpdatesSlackSettings()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingPrefs = new NotificationPreferences(userId);
        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPrefs);

        var command = new UpdateSlackPreferencesCommand(
            userId,
            SlackEnabled: true,
            SlackOnDocumentReady: false,
            SlackOnDocumentFailed: true,
            SlackOnRetryAvailable: false,
            SlackOnGameNightInvitation: true,
            SlackOnGameNightReminder: false,
            SlackOnShareRequestCreated: true,
            SlackOnShareRequestApproved: false,
            SlackOnBadgeEarned: true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(existingPrefs.SlackEnabled);
        Assert.False(existingPrefs.SlackOnDocumentReady);
        Assert.True(existingPrefs.SlackOnDocumentFailed);
        Assert.False(existingPrefs.SlackOnRetryAvailable);
        Assert.True(existingPrefs.SlackOnGameNightInvitation);
        Assert.False(existingPrefs.SlackOnGameNightReminder);
        Assert.True(existingPrefs.SlackOnShareRequestCreated);
        Assert.False(existingPrefs.SlackOnShareRequestApproved);
        Assert.True(existingPrefs.SlackOnBadgeEarned);

        _repositoryMock.Verify(r => r.UpdateAsync(existingPrefs, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoExistingPreferences_CreatesNewAndUpdates()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var command = new UpdateSlackPreferencesCommand(
            userId,
            SlackEnabled: true,
            SlackOnDocumentReady: true,
            SlackOnDocumentFailed: true,
            SlackOnRetryAvailable: false,
            SlackOnGameNightInvitation: true,
            SlackOnGameNightReminder: true,
            SlackOnShareRequestCreated: true,
            SlackOnShareRequestApproved: true,
            SlackOnBadgeEarned: true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            r => r.AddAsync(It.Is<NotificationPreferences>(p =>
                p.SlackEnabled &&
                p.SlackOnDocumentReady),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
