using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class StreamJobUpdatesQueryHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IQueueStreamService> _streamServiceMock = new();
    private readonly StreamJobUpdatesQueryHandler _handler;

    public StreamJobUpdatesQueryHandlerTests()
    {
        var loggerMock = new Mock<ILogger<StreamJobUpdatesQueryHandler>>();
        _handler = new StreamJobUpdatesQueryHandler(
            _jobRepositoryMock.Object, _streamServiceMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_JobNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobRepositoryMock
            .Setup(r => r.ExistsAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var act = async () =>
        {
            await foreach (var _ in _handler.Handle(new StreamJobUpdatesQuery(jobId), CancellationToken.None))
            {
                // Should not reach here
            }
        };

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_JobExists_DelegatesToStreamService()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobRepositoryMock
            .Setup(r => r.ExistsAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var events = new List<QueueStreamEvent>
        {
            new(QueueStreamEventType.JobStarted, jobId, null, DateTimeOffset.UtcNow),
            new(QueueStreamEventType.JobCompleted, jobId, null, DateTimeOffset.UtcNow)
        };

        _streamServiceMock
            .Setup(s => s.SubscribeToJob(jobId, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(events));

        // Act
        var received = new List<QueueStreamEvent>();
        await foreach (var evt in _handler.Handle(new StreamJobUpdatesQuery(jobId), CancellationToken.None))
        {
            received.Add(evt);
        }

        // Assert
        received.Should().HaveCount(2);
        received[0].Type.Should().Be(QueueStreamEventType.JobStarted);
        received[1].Type.Should().Be(QueueStreamEventType.JobCompleted);
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
