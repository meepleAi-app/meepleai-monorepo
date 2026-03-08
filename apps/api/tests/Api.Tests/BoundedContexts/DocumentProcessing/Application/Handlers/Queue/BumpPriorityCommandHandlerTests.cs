using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class BumpPriorityCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly BumpPriorityCommandHandler _handler;

    public BumpPriorityCommandHandlerTests()
    {
        _handler = new BumpPriorityCommandHandler(
            _jobRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesPriorityAndSaves()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), (int)ProcessingPriority.Normal, 0);
        var command = new BumpPriorityCommand(jobId, ProcessingPriority.Urgent);

        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _jobRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ProcessingJob>(j => j.Priority == (int)ProcessingPriority.Urgent),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_JobNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var command = new BumpPriorityCommand(Guid.NewGuid(), ProcessingPriority.High);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(command.JobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingJob?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProcessingJob_ThrowsInvalidOperation()
    {
        // Arrange: Create a job and start it so it's Processing
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), (int)ProcessingPriority.Normal, 0);
        job.Start();

        var command = new BumpPriorityCommand(job.Id, ProcessingPriority.Urgent);
        _jobRepositoryMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act & Assert — can only bump queued jobs
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
