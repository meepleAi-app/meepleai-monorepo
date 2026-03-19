using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Unit tests for GetUnreadCountQueryHandler.
/// Tests notification unread count retrieval scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public class GetUnreadCountQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _mockNotificationRepository;
    private readonly GetUnreadCountQueryHandler _handler;

    public GetUnreadCountQueryHandlerTests()
    {
        _mockNotificationRepository = new Mock<INotificationRepository>();
        _handler = new GetUnreadCountQueryHandler(_mockNotificationRepository.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetUnreadCountQueryHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("notificationRepository");
    }

    #endregion

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Successful Query Tests

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsUnreadCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedCount = 5;
        var query = new GetUnreadCountQuery(userId);

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(expectedCount);
        _mockNotificationRepository.Verify(
            r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoUnreadNotifications_ReturnsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUnreadCountQuery(userId);

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithLargeUnreadCount_ReturnsCorrectCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var largeCount = 9999;
        var query = new GetUnreadCountQuery(userId);

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(largeCount);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(largeCount);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUnreadCountQuery(userId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(userId, token))
            .ReturnsAsync(3);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _mockNotificationRepository.Verify(
            r => r.GetUnreadCountAsync(userId, token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUnreadCountQuery(userId);
        cts.Cancel();

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = () => _handler.Handle(query, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Repository Interaction Tests

    [Fact]
    public async Task Handle_CallsRepositoryWithCorrectUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUnreadCountQuery(userId);

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockNotificationRepository.Verify(
            r => r.GetUnreadCountAsync(
                It.Is<Guid>(id => id == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotCallRepositoryMultipleTimes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUnreadCountQuery(userId);

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockNotificationRepository.Verify(
            r => r.GetUnreadCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Query Validation Tests

    [Fact]
    public async Task Handle_WithEmptyUserId_StillCallsRepository()
    {
        // Arrange - Empty GUID is technically valid, validation is at command level
        var query = new GetUnreadCountQuery(Guid.Empty);

        _mockNotificationRepository
            .Setup(r => r.GetUnreadCountAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(0);
        _mockNotificationRepository.Verify(
            r => r.GetUnreadCountAsync(Guid.Empty, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
