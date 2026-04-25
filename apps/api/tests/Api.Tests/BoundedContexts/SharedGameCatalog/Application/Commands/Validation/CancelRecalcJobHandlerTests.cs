using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CancelRecalcJobHandlerTests
{
    private readonly Mock<IMechanicRecalcJobRepository> _jobRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<CancelRecalcJobHandler>> _loggerMock = new();

    private readonly CancelRecalcJobHandler _handler;

    public CancelRecalcJobHandlerTests()
    {
        _handler = new CancelRecalcJobHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullJobRepository_Throws()
    {
        var act = () => new CancelRecalcJobHandler(
            jobRepository: null!,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("jobRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new CancelRecalcJobHandler(
            _jobRepoMock.Object,
            unitOfWork: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new CancelRecalcJobHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Handle_WithNullRequest_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task Handle_JobNotFound_ThrowsNotFoundAndDoesNotPersist()
    {
        var jobId = Guid.NewGuid();
        _jobRepoMock
            .Setup(r => r.GetByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicRecalcJob?)null);

        var act = () => _handler.Handle(
            new CancelRecalcJobCommand(jobId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();

        _jobRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PendingJob_SetsCancellationFlagAndPersists()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var sequence = new List<string>();
        _jobRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("repo.UpdateAsync"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("uow.Save"))
            .ReturnsAsync(1);

        var result = await _handler.Handle(
            new CancelRecalcJobCommand(job.Id),
            TestContext.Current.CancellationToken);

        result.Should().Be(MediatR.Unit.Value);

        // Cancellation is a flag, not a status — Pending stays Pending.
        job.Status.Should().Be(RecalcJobStatus.Pending);
        job.CancellationRequested.Should().BeTrue();

        sequence.Should().Equal("repo.UpdateAsync", "uow.Save");
        _jobRepoMock.Verify(r => r.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RunningJob_SetsCancellationFlagAndPersists()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.MarkRunning(total: 10);

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(
            new CancelRecalcJobCommand(job.Id),
            TestContext.Current.CancellationToken);

        job.Status.Should().Be(RecalcJobStatus.Running);
        job.CancellationRequested.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_IsIdempotentNoConflict()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.RequestCancellation();
        job.CancellationRequested.Should().BeTrue();

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);
        _jobRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var act = () => _handler.Handle(
            new CancelRecalcJobCommand(job.Id),
            TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        job.CancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CompletedJob_ThrowsConflictAndDoesNotPersist()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.MarkRunning(total: 1);
        job.RecordSuccess(Guid.NewGuid());
        job.Complete();
        job.Status.Should().Be(RecalcJobStatus.Completed);

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var act = () => _handler.Handle(
            new CancelRecalcJobCommand(job.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{job.Id}*Completed*");

        _jobRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FailedJob_ThrowsConflict()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.Fail("something broke");
        job.Status.Should().Be(RecalcJobStatus.Failed);

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var act = () => _handler.Handle(
            new CancelRecalcJobCommand(job.Id),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>();

        _jobRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
