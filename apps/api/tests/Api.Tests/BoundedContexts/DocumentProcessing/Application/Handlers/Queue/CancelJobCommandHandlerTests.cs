using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
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

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class CancelJobCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CancelJobCommandHandler _handler;

    public CancelJobCommandHandlerTests()
    {
        _handler = new CancelJobCommandHandler(
            _jobRepositoryMock.Object, _unitOfWorkMock.Object, _timeProvider);
    }

    [Fact]
    public async Task Handle_QueuedJob_CancelsSuccessfully()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        await _handler.Handle(new CancelJobCommand(job.Id), CancellationToken.None);

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
        _jobRepositoryMock.Verify(
            r => r.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProcessingJob_CancelsSuccessfully()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        job.Start(_timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        await _handler.Handle(new CancelJobCommand(job.Id), CancellationToken.None);

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
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
        await FluentActions.Invoking(() => _handler.Handle(new CancelJobCommand(jobId), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CompletedJob_ThrowsInvalidOperationException()
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
        await FluentActions.Invoking(() => _handler.Handle(new CancelJobCommand(job.Id), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
