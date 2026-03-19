using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
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
public sealed class RemoveFromQueueCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly RemoveFromQueueCommandHandler _handler;

    public RemoveFromQueueCommandHandlerTests()
    {
        _handler = new RemoveFromQueueCommandHandler(
            _jobRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_QueuedJob_RemovesSuccessfully()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        await _handler.Handle(new RemoveFromQueueCommand(job.Id), CancellationToken.None);

        // Assert
        _jobRepositoryMock.Verify(
            r => r.DeleteAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingJob?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(new RemoveFromQueueCommand(jobId), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProcessingJob_ThrowsConflictException()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        job.Start(_timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(new RemoveFromQueueCommand(job.Id), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_CompletedJob_ThrowsConflictException()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        job.Start(_timeProvider);
        job.Complete(_timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(new RemoveFromQueueCommand(job.Id), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
    }
}
