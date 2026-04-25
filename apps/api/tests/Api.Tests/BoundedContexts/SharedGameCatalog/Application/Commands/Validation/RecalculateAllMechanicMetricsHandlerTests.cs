using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RecalculateAllMechanicMetricsHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _analysisRepoMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ILogger<RecalculateAllMechanicMetricsHandler>> _loggerMock = new();

    private readonly RecalculateAllMechanicMetricsHandler _handler;

    public RecalculateAllMechanicMetricsHandlerTests()
    {
        _handler = new RecalculateAllMechanicMetricsHandler(
            _analysisRepoMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    // ============================================================================================
    // Constructor null-argument tests
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullAnalysisRepository_Throws()
    {
        var act = () => new RecalculateAllMechanicMetricsHandler(
            analysisRepository: null!,
            _mediatorMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("analysisRepository");
    }

    [Fact]
    public void Constructor_WithNullMediator_Throws()
    {
        var act = () => new RecalculateAllMechanicMetricsHandler(
            _analysisRepoMock.Object,
            mediator: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediator");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new RecalculateAllMechanicMetricsHandler(
            _analysisRepoMock.Object,
            _mediatorMock.Object,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ============================================================================================
    // Handle argument-guard
    // ============================================================================================

    [Fact]
    public async Task Handle_WithNullRequest_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    // ============================================================================================
    // Empty-candidate set
    // ============================================================================================

    [Fact]
    public async Task Handle_NoPublishedAnalyses_ReturnsZero_AndDispatchesNothing()
    {
        _analysisRepoMock
            .Setup(r => r.GetIdsByStatusAsync(
                MechanicAnalysisStatus.Published,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var result = await _handler.Handle(
            new RecalculateAllMechanicMetricsCommand(),
            TestContext.Current.CancellationToken);

        result.Should().Be(0);

        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CalculateMechanicAnalysisMetricsCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ============================================================================================
    // Happy path
    // ============================================================================================

    [Fact]
    public async Task Handle_AllSucceed_ReturnsCandidateCount()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _analysisRepoMock
            .Setup(r => r.GetIdsByStatusAsync(
                MechanicAnalysisStatus.Published,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        _mediatorMock
            .Setup(m => m.Send(
                It.IsAny<CalculateMechanicAnalysisMetricsCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var result = await _handler.Handle(
            new RecalculateAllMechanicMetricsCommand(),
            TestContext.Current.CancellationToken);

        result.Should().Be(3);

        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CalculateMechanicAnalysisMetricsCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        // Each id was dispatched exactly once (in enumeration order — though order is not asserted).
        foreach (var id in ids)
        {
            _mediatorMock.Verify(
                m => m.Send(
                    It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId == id),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    // ============================================================================================
    // Per-id failure isolation: NotFoundException + ConflictException are swallowed-and-logged
    // ============================================================================================

    [Fact]
    public async Task Handle_OneIdThrowsNotFound_ContinuesAndReturnsSurvivors()
    {
        var failingId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), failingId, Guid.NewGuid() };
        _analysisRepoMock
            .Setup(r => r.GetIdsByStatusAsync(
                MechanicAnalysisStatus.Published,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId == failingId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("MechanicAnalysis", failingId.ToString()));

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId != failingId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var result = await _handler.Handle(
            new RecalculateAllMechanicMetricsCommand(),
            TestContext.Current.CancellationToken);

        result.Should().Be(2, "the failing id is skipped, the remaining two succeed");

        // All 3 ids were attempted — the failure did not abort the loop.
        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CalculateMechanicAnalysisMetricsCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_OneIdThrowsConflict_ContinuesAndReturnsSurvivors()
    {
        var failingId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), failingId, Guid.NewGuid() };
        _analysisRepoMock
            .Setup(r => r.GetIdsByStatusAsync(
                MechanicAnalysisStatus.Published,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId == failingId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(
                $"MechanicAnalysis {failingId} must be in Published status to calculate metrics."));

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId != failingId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var result = await _handler.Handle(
            new RecalculateAllMechanicMetricsCommand(),
            TestContext.Current.CancellationToken);

        result.Should().Be(2);

        _mediatorMock.Verify(
            m => m.Send(
                It.IsAny<CalculateMechanicAnalysisMetricsCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ============================================================================================
    // Unexpected exception aborts the batch
    // ============================================================================================

    [Fact]
    public async Task Handle_UnexpectedExceptionAborts()
    {
        var failingId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), failingId, Guid.NewGuid() };
        _analysisRepoMock
            .Setup(r => r.GetIdsByStatusAsync(
                MechanicAnalysisStatus.Published,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        // First id succeeds; second id throws an unexpected exception type that the handler
        // does not catch — the loop must abort and the exception must propagate.
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId == ids[0]),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CalculateMechanicAnalysisMetricsCommand>(c => c.MechanicAnalysisId == failingId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected infrastructure failure"));

        var act = () => _handler.Handle(
            new RecalculateAllMechanicMetricsCommand(),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("unexpected infrastructure failure");
    }
}
