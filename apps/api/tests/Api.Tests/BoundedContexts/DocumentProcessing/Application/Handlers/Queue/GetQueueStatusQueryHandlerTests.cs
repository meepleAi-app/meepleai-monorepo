using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class GetQueueStatusQueryHandlerTests
{
    private readonly Mock<IQueueBackpressureService> _backpressureMock = new();
    private readonly Mock<IProcessingQueueConfigRepository> _configRepoMock = new();
    private readonly GetQueueStatusQueryHandler _handler;

    public GetQueueStatusQueryHandlerTests()
    {
        _handler = new GetQueueStatusQueryHandler(
            _backpressureMock.Object,
            _configRepoMock.Object);
    }

    [Fact]
    public async Task Handle_EmptyQueue_ReturnsNoBackpressure()
    {
        _backpressureMock
            .Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackpressureResult(0, 50, false, TimeSpan.Zero, 3));
        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessingQueueConfig.CreateDefault());

        var result = await _handler.Handle(new GetQueueStatusQuery(), CancellationToken.None);

        result.QueueDepth.Should().Be(0);
        result.IsUnderPressure.Should().BeFalse();
        result.IsPaused.Should().BeFalse();
        result.EstimatedWaitMinutes.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FullQueue_ReturnsBackpressureWithEta()
    {
        _backpressureMock
            .Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackpressureResult(60, 50, true, TimeSpan.FromMinutes(60), 3));
        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessingQueueConfig.CreateDefault());

        var result = await _handler.Handle(new GetQueueStatusQuery(), CancellationToken.None);

        result.QueueDepth.Should().Be(60);
        result.IsUnderPressure.Should().BeTrue();
        result.EstimatedWaitMinutes.Should().Be(60);
    }

    [Fact]
    public async Task Handle_PausedQueue_ReportsPaused()
    {
        _backpressureMock
            .Setup(s => s.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackpressureResult(5, 50, false, TimeSpan.FromMinutes(5), 3));

        var config = ProcessingQueueConfig.CreateDefault();
        config.Update(isPaused: true, maxConcurrentWorkers: null, updatedBy: Guid.NewGuid());
        _configRepoMock
            .Setup(r => r.GetOrCreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _handler.Handle(new GetQueueStatusQuery(), CancellationToken.None);

        result.IsPaused.Should().BeTrue();
    }
}
