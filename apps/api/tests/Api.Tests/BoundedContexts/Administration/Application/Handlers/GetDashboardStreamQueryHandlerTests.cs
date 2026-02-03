using System.Runtime.CompilerServices;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Administration.Domain.Services;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Unit tests for GetDashboardStreamQueryHandler (Issue #3324).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class GetDashboardStreamQueryHandlerTests
{
    private readonly Mock<IDashboardStreamService> _mockStreamService;
    private readonly GetDashboardStreamQueryHandler _handler;

    public GetDashboardStreamQueryHandlerTests()
    {
        _mockStreamService = new Mock<IDashboardStreamService>();
        _handler = new GetDashboardStreamQueryHandler(_mockStreamService.Object);
    }

    [Fact]
    public async Task Handle_ReturnsAsyncEnumerableFromService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardStreamQuery(userId);

        var expectedEvent = new DashboardStatsUpdatedEvent { CollectionCount = 10, PlayedCount = 5 };

        _mockStreamService
            .Setup(s => s.SubscribeToDashboardEvents(userId, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(expectedEvent));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var resultList = new List<INotification>();
        await foreach (var evt in result)
        {
            resultList.Add(evt);
        }
        resultList.Should().HaveCount(1);
        resultList[0].Should().BeOfType<DashboardStatsUpdatedEvent>();
    }

    [Fact]
    public async Task Handle_PassesUserIdToService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardStreamQuery(userId);
        Guid capturedUserId = Guid.Empty;

        _mockStreamService
            .Setup(s => s.SubscribeToDashboardEvents(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((uid, _) => capturedUserId = uid)
            .Returns(CreateEmptyAsyncEnumerable());

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedUserId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetDashboardStreamQuery(userId);
        using var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;

        _mockStreamService
            .Setup(s => s.SubscribeToDashboardEvents(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((_, ct) => capturedToken = ct)
            .Returns(CreateEmptyAsyncEnumerable());

        // Act
        await _handler.Handle(query, cts.Token);

        // Assert
        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public void Constructor_ThrowsOnNullStreamService()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetDashboardStreamQueryHandler(null!));
    }

    // Helper methods to create async enumerables
    private static async IAsyncEnumerable<INotification> CreateAsyncEnumerable(
        params INotification[] events)
    {
        foreach (var evt in events)
        {
            yield return evt;
        }
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<INotification> CreateEmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}
