using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class StreamQueueUpdatesQueryHandlerTests
{
    private readonly Mock<IQueueStreamService> _streamServiceMock = new();
    private readonly StreamQueueUpdatesQueryHandler _handler;

    public StreamQueueUpdatesQueryHandlerTests()
    {
        var loggerMock = new Mock<ILogger<StreamQueueUpdatesQueryHandler>>();
        _handler = new StreamQueueUpdatesQueryHandler(_streamServiceMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_DelegatesToStreamService()
    {
        // Arrange
        var events = new List<QueueStreamEvent>
        {
            new(QueueStreamEventType.JobQueued, Guid.NewGuid(), null, DateTimeOffset.UtcNow),
            new(QueueStreamEventType.JobStarted, Guid.NewGuid(), null, DateTimeOffset.UtcNow),
            new(QueueStreamEventType.Heartbeat, Guid.Empty, null, DateTimeOffset.UtcNow)
        };

        _streamServiceMock
            .Setup(s => s.SubscribeToQueue(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(events));

        // Act
        var received = new List<QueueStreamEvent>();
        await foreach (var evt in _handler.Handle(new StreamQueueUpdatesQuery(), CancellationToken.None))
        {
            received.Add(evt);
        }

        // Assert
        received.Should().HaveCount(3);
        received[0].Type.Should().Be(QueueStreamEventType.JobQueued);
        received[1].Type.Should().Be(QueueStreamEventType.JobStarted);
        received[2].Type.Should().Be(QueueStreamEventType.Heartbeat);
    }

#pragma warning disable CS1998
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
#pragma warning restore CS1998
}
