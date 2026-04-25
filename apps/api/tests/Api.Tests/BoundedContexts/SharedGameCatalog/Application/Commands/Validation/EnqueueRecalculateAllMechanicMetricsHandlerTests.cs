using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class EnqueueRecalculateAllMechanicMetricsHandlerTests
{
    private readonly Mock<IMechanicRecalcJobRepository> _jobRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<EnqueueRecalculateAllMechanicMetricsHandler>> _loggerMock = new();

    private readonly EnqueueRecalculateAllMechanicMetricsHandler _handler;

    public EnqueueRecalculateAllMechanicMetricsHandlerTests()
    {
        _handler = new EnqueueRecalculateAllMechanicMetricsHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullJobRepository_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
            jobRepository: null!,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("jobRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
            _jobRepoMock.Object,
            unitOfWork: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
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
    public async Task Handle_HappyPath_PersistsPendingJobAndReturnsId()
    {
        // Arrange — capture the aggregate handed to AddAsync so we can assert on its initial state.
        var userId = Guid.NewGuid();

        MechanicRecalcJob? captured = null;
        var sequence = new List<string>();
        _jobRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()))
            .Callback<MechanicRecalcJob, CancellationToken>((j, _) =>
            {
                captured = j;
                sequence.Add("repo.AddAsync");
            })
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("uow.Save"))
            .ReturnsAsync(1);

        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await _handler.Handle(
            new EnqueueRecalculateAllMechanicMetricsCommand(userId),
            TestContext.Current.CancellationToken);

        var after = DateTimeOffset.UtcNow;

        // Assert — returned id matches the persisted aggregate's id.
        captured.Should().NotBeNull();
        result.Should().Be(captured!.Id);
        result.Should().NotBe(Guid.Empty);

        // Initial aggregate state matches the Pending invariants the worker expects to claim.
        captured.Status.Should().Be(RecalcJobStatus.Pending);
        captured.TriggeredByUserId.Should().Be(userId);
        captured.Total.Should().Be(0);
        captured.Processed.Should().Be(0);
        captured.Failed.Should().Be(0);
        captured.Skipped.Should().Be(0);
        captured.ConsecutiveFailures.Should().Be(0);
        captured.LastError.Should().BeNull();
        captured.LastProcessedAnalysisId.Should().BeNull();
        captured.CancellationRequested.Should().BeFalse();
        captured.StartedAt.Should().BeNull();
        captured.CompletedAt.Should().BeNull();
        captured.HeartbeatAt.Should().BeNull();
        captured.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        // Persistence order: AddAsync then SaveChanges (single UoW commit).
        sequence.Should().Equal("repo.AddAsync", "uow.Save");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PropagatesAggregateGuard_WhenUserIdIsEmpty()
    {
        // The factory rejects Guid.Empty defensively even though the validator should catch it
        // at the application boundary first. This exercises the defense-in-depth path.
        var act = () => _handler.Handle(
            new EnqueueRecalculateAllMechanicMetricsCommand(Guid.Empty),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TriggeredByUserId cannot be empty*");

        _jobRepoMock.Verify(
            r => r.AddAsync(It.IsAny<MechanicRecalcJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
