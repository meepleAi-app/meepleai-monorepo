using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
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
    // AuditService.LogAsync is virtual; we mock the class directly. The DB context arg is unused
    // by the production handler path because we override LogAsync via the mock — passing null! is
    // safe because the constructor only stores references.
    private readonly Mock<AuditService> _auditServiceMock = new(
        MockBehavior.Loose,
        null!,
        Mock.Of<ILogger<AuditService>>(),
        null!);
    private readonly Mock<ILogger<EnqueueRecalculateAllMechanicMetricsHandler>> _loggerMock = new();

    private readonly EnqueueRecalculateAllMechanicMetricsHandler _handler;

    public EnqueueRecalculateAllMechanicMetricsHandlerTests()
    {
        _handler = new EnqueueRecalculateAllMechanicMetricsHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object,
            _auditServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullJobRepository_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
            jobRepository: null!,
            _unitOfWorkMock.Object,
            _auditServiceMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("jobRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
            _jobRepoMock.Object,
            unitOfWork: null!,
            _auditServiceMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullAuditService_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object,
            auditService: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("auditService");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new EnqueueRecalculateAllMechanicMetricsHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object,
            _auditServiceMock.Object,
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

        _auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("audit.LogAsync"))
            .Returns(Task.CompletedTask);

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

        // Persistence order: AddAsync → SaveChanges → audit (audit happens after primary commit
        // so it never aborts the command on failure).
        sequence.Should().Equal("repo.AddAsync", "uow.Save", "audit.LogAsync");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Audit entry — verifies ADR-051 Sprint 2 / Task 12 acceptance criteria.
        _auditServiceMock.Verify(a => a.LogAsync(
            userId.ToString(),
            "mechanic_recalc.enqueued",
            "MechanicRecalcJob",
            captured.Id.ToString(),
            "Success",
            It.Is<string?>(d => d != null && d.Contains(captured.Id.ToString())),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
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
        _auditServiceMock.Verify(a => a.LogAsync(
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
