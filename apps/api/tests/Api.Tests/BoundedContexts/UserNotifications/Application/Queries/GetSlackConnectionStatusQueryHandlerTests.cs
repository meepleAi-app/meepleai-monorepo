using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Queries;

[Trait("Category", TestCategories.Unit)]
public class GetSlackConnectionStatusQueryHandlerTests
{
    private readonly Mock<ISlackConnectionRepository> _connectionRepoMock;
    private readonly GetSlackConnectionStatusQueryHandler _handler;

    public GetSlackConnectionStatusQueryHandlerTests()
    {
        _connectionRepoMock = new Mock<ISlackConnectionRepository>();
        _handler = new GetSlackConnectionStatusQueryHandler(_connectionRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WithActiveConnection_ReturnsConnectedStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connection = SlackConnection.Create(userId, "U123", "T123", "Test Team", "xoxb-token", "D123");
        _connectionRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var query = new GetSlackConnectionStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsConnected);
        Assert.Equal("Test Team", result.SlackTeamName);
        Assert.Equal("U123", result.SlackUserId);
        Assert.NotNull(result.ConnectedAt);
    }

    [Fact]
    public async Task Handle_WithDisconnectedConnection_ReturnsDisconnectedStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connection = SlackConnection.Create(userId, "U123", "T123", "Test Team", "xoxb-token", "D123");
        connection.Disconnect(DateTime.UtcNow);
        _connectionRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var query = new GetSlackConnectionStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsConnected);
        Assert.Equal("Test Team", result.SlackTeamName);
    }

    [Fact]
    public async Task Handle_WithNoConnection_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _connectionRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlackConnection?)null);

        var query = new GetSlackConnectionStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
