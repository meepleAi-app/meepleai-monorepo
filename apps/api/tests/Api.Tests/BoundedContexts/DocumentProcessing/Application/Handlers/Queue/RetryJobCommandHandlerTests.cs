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
public sealed class RetryJobCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly RetryJobCommandHandler _handler;

    public RetryJobCommandHandlerTests()
    {
        _handler = new RetryJobCommandHandler(
            _jobRepositoryMock.Object, _unitOfWorkMock.Object, _timeProvider);
    }

    [Fact]
    public async Task Handle_FailedJob_RetriesSuccessfully()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        job.Start(_timeProvider);
        job.Fail("Test error", _timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        await _handler.Handle(new RetryJobCommand(job.Id), CancellationToken.None);

        // Assert
        job.Status.Should().Be(JobStatus.Queued);
        job.RetryCount.Should().Be(1);
        _jobRepositoryMock.Verify(
            r => r.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
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
        await FluentActions.Invoking(() => _handler.Handle(new RetryJobCommand(jobId), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_QueuedJob_ThrowsConflictException()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act & Assert - ConflictException (409) per CLAUDE.md #2568 conventions
        await FluentActions.Invoking(() => _handler.Handle(new RetryJobCommand(job.Id), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_MaxRetriesExceeded_ThrowsConflictException()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);

        // Exhaust all retries
        for (var i = 0; i < ProcessingJob.DefaultMaxRetries; i++)
        {
            job.Start(_timeProvider);
            job.Fail($"Error {i}", _timeProvider);
            job.Retry(_timeProvider);
        }

        // Fail one more time - now at max retries
        job.Start(_timeProvider);
        job.Fail("Final error", _timeProvider);
        job.ClearDomainEvents();

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act & Assert - ConflictException (409) per CLAUDE.md #2568 conventions
        await FluentActions.Invoking(() => _handler.Handle(new RetryJobCommand(job.Id), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>()
            .WithMessage("*maximum retries*");
    }
}
