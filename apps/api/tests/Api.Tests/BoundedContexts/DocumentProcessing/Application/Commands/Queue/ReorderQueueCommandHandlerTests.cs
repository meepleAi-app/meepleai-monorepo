using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class ReorderQueueCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ReorderQueueCommandHandler _handler;

    public ReorderQueueCommandHandlerTests()
    {
        _handler = new ReorderQueueCommandHandler(
            _jobRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_QueuedJobs_ReordersSuccessfully()
    {
        // Arrange
        var job1 = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 5, 0, _timeProvider);
        var job2 = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 3, 0, _timeProvider);
        var job3 = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 0, _timeProvider);

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job1);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job2);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job3.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job3);

        // Reorder: job3 first, then job1, then job2
        var orderedIds = new List<Guid> { job3.Id, job1.Id, job2.Id };

        // Act
        await _handler.Handle(new ReorderQueueCommand(orderedIds), CancellationToken.None);

        // Assert - priorities should match position in list
        job3.Priority.Should().Be(0);
        job1.Priority.Should().Be(1);
        job2.Priority.Should().Be(2);

        _jobRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var existingJob = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        var missingId = Guid.NewGuid();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(existingJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingJob);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingJob?)null);

        var orderedIds = new List<Guid> { existingJob.Id, missingId };

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(new ReorderQueueCommand(orderedIds), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NonQueuedJob_ThrowsConflictException()
    {
        // Arrange
        var queuedJob = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        var processingJob = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        processingJob.Start(_timeProvider);

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(queuedJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queuedJob);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(processingJob.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingJob);

        var orderedIds = new List<Guid> { queuedJob.Id, processingJob.Id };

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(new ReorderQueueCommand(orderedIds), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_SingleJob_SetsCorrectPriority()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 10, 0, _timeProvider);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var orderedIds = new List<Guid> { job.Id };

        // Act
        await _handler.Handle(new ReorderQueueCommand(orderedIds), CancellationToken.None);

        // Assert
        job.Priority.Should().Be(0);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
