using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// #4503: il batch che elenca le analisi meccaniche rimaste in <c>Draft</c> oltre la soglia e manda un comando per id.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ExpireStaleMechanicAnalysisDraftsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMechanicAnalysisRepository> _repository = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly FakeTimeProvider _time = new(Now);

    private ExpireStaleMechanicAnalysisDraftsCommandHandler CreateHandler(TimeSpan? threshold = null) =>
        new(
            _repository.Object,
            _mediator.Object,
            _time,
            Options.Create(new MechanicAnalysisStaleDraftOptions
            {
                Threshold = threshold ?? MechanicAnalysisStaleDraftOptions.DefaultThreshold,
            }),
            NullLogger<ExpireStaleMechanicAnalysisDraftsCommandHandler>.Instance);

    private void ReturnsIds(params Guid[] ids) =>
        _repository
            .Setup(r => r.GetDraftIdsCreatedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

    [Fact]
    public async Task Handle_AsksForDraftsOlderThanNowMinusThreshold_OneBatchAtMost()
    {
        ReturnsIds();

        await CreateHandler(TimeSpan.FromHours(3))
            .Handle(new ExpireStaleMechanicAnalysisDraftsCommand(), CancellationToken.None);

        _repository.Verify(
            r => r.GetDraftIdsCreatedBeforeAsync(
                Now.UtcDateTime - TimeSpan.FromHours(3),
                MechanicAnalysisStaleDraftOptions.BatchSize,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SendsOneCommandPerId_AndCountsThem()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ReturnsIds(ids);

        var result = await CreateHandler()
            .Handle(new ExpireStaleMechanicAnalysisDraftsCommand(), CancellationToken.None);

        result.Should().Be(new ExpireStaleDraftsResult(Found: 3, Expired: 3));
        foreach (var id in ids)
        {
            _mediator.Verify(
                m => m.Send(
                    It.Is<ExpireStaleMechanicAnalysisDraftCommand>(c => c.AnalysisId == id),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Handle_SkipsARowThatIsGoneOrLeftDraft_AndKeepsGoing()
    {
        var gone = Guid.NewGuid();
        var moved = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        ReturnsIds(gone, moved, orphan);
        _mediator
            .Setup(m => m.Send(It.Is<ExpireStaleMechanicAnalysisDraftCommand>(c => c.AnalysisId == gone), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("MechanicAnalysis", gone.ToString()));
        _mediator
            .Setup(m => m.Send(It.Is<ExpireStaleMechanicAnalysisDraftCommand>(c => c.AnalysisId == moved), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("no longer Draft"));

        var result = await CreateHandler()
            .Handle(new ExpireStaleMechanicAnalysisDraftsCommand(), CancellationToken.None);

        result.Should().Be(new ExpireStaleDraftsResult(Found: 3, Expired: 1));
        _mediator.Verify(
            m => m.Send(
                It.Is<ExpireStaleMechanicAnalysisDraftCommand>(c => c.AnalysisId == orphan),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LetsAnInfrastructureFailurePropagate_AndStopsTheBatch()
    {
        // Un SaveChanges fallito lascia lo stato nel DbContext dello scope: andare avanti con le scritture
        // successive nello stesso scope le farebbe fallire con lui. Il job registra l'errore e il giro dopo
        // riparte da uno scope nuovo.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        ReturnsIds(first, second);
        _mediator
            .Setup(m => m.Send(It.Is<ExpireStaleMechanicAnalysisDraftCommand>(c => c.AnalysisId == first), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateHandler()
            .Handle(new ExpireStaleMechanicAnalysisDraftsCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("db down");
        _mediator.Verify(
            m => m.Send(
                It.Is<ExpireStaleMechanicAnalysisDraftCommand>(c => c.AnalysisId == second),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task Handle_WithAThresholdThatIsNotPositive_ExpiresNothingAndDoesNotEvenQuery(int minutes)
    {
        // Zero o meno renderebbe "scaduto" ogni Draft, quelli di una pipeline viva compresi.
        ReturnsIds(Guid.NewGuid());

        var result = await CreateHandler(TimeSpan.FromMinutes(minutes))
            .Handle(new ExpireStaleMechanicAnalysisDraftsCommand(), CancellationToken.None);

        result.Should().Be(new ExpireStaleDraftsResult(Found: 0, Expired: 0));
        _repository.Verify(
            r => r.GetDraftIdsCreatedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(
            m => m.Send(It.IsAny<ExpireStaleMechanicAnalysisDraftCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNoStaleDrafts_SendsNothing()
    {
        ReturnsIds();

        var result = await CreateHandler()
            .Handle(new ExpireStaleMechanicAnalysisDraftsCommand(), CancellationToken.None);

        result.Should().Be(new ExpireStaleDraftsResult(Found: 0, Expired: 0));
        _mediator.Verify(
            m => m.Send(It.IsAny<ExpireStaleMechanicAnalysisDraftCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void DefaultThreshold_IsTwoHours()
    {
        // Il valore e' un giudizio dichiarato nella PR di #4503, non una misura: se cambia, cambi qui e li'.
        new MechanicAnalysisStaleDraftOptions().Threshold.Should().Be(TimeSpan.FromHours(2));
    }
}
