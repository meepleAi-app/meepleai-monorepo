using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Services;
using Api.Tests.BoundedContexts.UserNotifications.TestHelpers;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Tests for GetNotificationsQueryHandler.
/// Verifies notification retrieval, filtering, and configurable pagination limits.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetNotificationsQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly GetNotificationsQueryHandler _handler;

    private const int DefaultMaxPageSize = 50;

    public GetNotificationsQueryHandlerTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _configServiceMock = new Mock<IConfigurationService>();

        // Default configuration: return 50 as max page size
        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>("Notifications:MaxPageSize", It.IsAny<int?>(), null))
            .ReturnsAsync(DefaultMaxPageSize);

        _handler = new GetNotificationsQueryHandler(
            _notificationRepositoryMock.Object,
            _configServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNotificationsForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            new NotificationBuilder().WithUserId(userId).WithTitle("Notification 1").Build(),
            new NotificationBuilder().WithUserId(userId).WithTitle("Notification 2").Build()
        };

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var query = new GetNotificationsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Count.Should().Be(2);
        result[0].Title.Should().Be("Notification 1");
        result[1].Title.Should().Be("Notification 2");
    }

    [Fact]
    public async Task Handle_WithUnreadOnlyFilter_FiltersCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var unreadNotifications = new List<Notification>
        {
            new NotificationBuilder().WithUserId(userId).Build()
        };

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, true, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unreadNotifications);

        var query = new GetNotificationsQuery(userId, UnreadOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, true, DefaultMaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithRequestedLimitBelowMax_UsesRequestedLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestedLimit = 10;

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, requestedLimit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId, Limit: requestedLimit);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, requestedLimit, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithRequestedLimitAboveMax_UsesConfiguredMax()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configuredMax = 30;
        var requestedLimit = 100; // Above configured max

        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>("Notifications:MaxPageSize", It.IsAny<int?>(), null))
            .ReturnsAsync(configuredMax);

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, configuredMax, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId, Limit: requestedLimit);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Should use configured max (30), not requested (100)
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, configuredMax, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullConfigValue_UsesDefaultMax()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>("Notifications:MaxPageSize", It.IsAny<int?>(), null))
            .ReturnsAsync((int?)null);

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Should use default (50)
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoLimitSpecified_UsesDefaultMax()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId); // No limit specified

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MapsNotificationsToDtosCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new NotificationBuilder()
            .WithId(notificationId)
            .WithUserId(userId)
            .WithTitle("Test Title")
            .WithMessage("Test Message")
            .WithLink("https://example.com")
            .Build();

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification> { notification });

        var query = new GetNotificationsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var dto = result.First();
        dto.Id.Should().Be(notificationId);
        dto.UserId.Should().Be(userId);
        dto.Title.Should().Be("Test Title");
        dto.Message.Should().Be("Test Message");
        dto.Link.Should().Be("https://example.com");
        dto.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, cancellationToken))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCustomConfiguredMax_EnforcesConfiguredLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var customMax = 25;

        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>("Notifications:MaxPageSize", It.IsAny<int?>(), null))
            .ReturnsAsync(customMax);

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, customMax, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId, Limit: 50); // Request more than configured

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Should cap at configured max (25)
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, customMax, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetNotificationsQueryHandler(null!, _configServiceMock.Object);
        var exception = act.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("notificationRepository");
    }

    [Fact]
    public async Task Constructor_WithNullConfigService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act2 = () =>
            new GetNotificationsQueryHandler(_notificationRepositoryMock.Object, null!);
        var exception = act2.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("configService");
    }

    [Fact]
    public async Task Handle_WithNegativeLimit_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetNotificationsQuery(userId, Limit: -1);

        // Act & Assert
        var act3 = () => _handler.Handle(query, CancellationToken.None);
        var exception = (await act3.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("non-negative");
        exception.ParamName.Should().Be("query");
    }

    [Fact]
    public async Task Handle_WithZeroLimit_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId, Limit: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Zero limit is valid and returns empty list
        result.Should().BeEmpty();
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNegativeConfigValue_UsesDefaultMax()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>("Notifications:MaxPageSize", It.IsAny<int?>(), null))
            .ReturnsAsync(-10); // Invalid negative config value

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Should sanitize to default (50)
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithZeroConfigValue_UsesDefaultMax()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _configServiceMock
            .Setup(c => c.GetValueAsync<int?>("Notifications:MaxPageSize", It.IsAny<int?>(), null))
            .ReturnsAsync(0); // Invalid zero config value

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Should sanitize to default (50)
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithVeryLargeLimit_UsesConfiguredMax()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var veryLargeLimit = int.MaxValue;

        _notificationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var query = new GetNotificationsQuery(userId, Limit: veryLargeLimit);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Should cap at configured max (50)
        _notificationRepositoryMock.Verify(
            r => r.GetByUserIdAsync(userId, false, DefaultMaxPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
