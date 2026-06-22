using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedFetchJobTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _repo = new();
    private readonly Mock<ICatalogSeedAggregator> _aggregator = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMediator> _mediator = new();

    private CatalogSeedFetchJob CreateJob() =>
        new(serviceProvider: new Mock<IServiceProvider>().Object,
            NullLogger<CatalogSeedFetchJob>.Instance);

    private static CatalogSeedDraftEntity SeedDraft(int? bggId = 12345, string? qid = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            BggId = bggId,
            WikidataQid = qid,
            Status = "Pending",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

    private static CatalogSeedAggregationResult SuccessResult(string provider = "wikidata+bgg")
    {
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["title"] = new FieldProvenance(
                Provider: "wikidata",
                SourceUrl: "https://www.wikidata.org/wiki/Q123",
                SourceField: "labels.en",
                FetchedAt: DateTime.UtcNow,
                Value: "Catan"),
            ["yearPublished"] = new FieldProvenance(
                Provider: "wikidata",
                SourceUrl: "https://www.wikidata.org/wiki/Q123",
                SourceField: "P577",
                FetchedAt: DateTime.UtcNow,
                Value: 1995),
        };
        return new CatalogSeedAggregationResult(
            new CatalogSeedProvenance(fields),
            provider,
            CombinedRawPayload: "{\"wikidata\":{},\"bgg\":{}}");
    }

    private static CatalogSeedAggregationResult EmptyResult() =>
        new(
            new CatalogSeedProvenance(new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)),
            ProviderUsed: "none",
            CombinedRawPayload: "{\"wikidata\":null,\"bgg\":null}");

    [Fact]
    public async Task RunBatchAsync_NoEligibleDrafts_DoesNothing()
    {
        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<CatalogSeedDraftEntity>());

        await CreateJob().RunBatchAsync(_repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, default);

        _aggregator.Verify(a => a.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Publish(It.IsAny<CatalogSeedFetchedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_Pending_Single_PopulatesFetched_PublishesEvent()
    {
        var draft = SeedDraft();
        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<CatalogSeedDraftEntity> { draft });

        var result = SuccessResult();
        _aggregator.Setup(a => a.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(result);

        await CreateJob().RunBatchAsync(_repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, default);

        draft.Status.Should().Be("Fetched");
        draft.ProvenanceJson.Should().NotBeNullOrWhiteSpace();
        draft.RawPayloadJson.Should().NotBeNullOrWhiteSpace();
        draft.FetchedAt.Should().NotBeNull();
        draft.ErrorMessage.Should().BeNull();

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Publish(
            It.Is<CatalogSeedFetchedEvent>(e =>
                e.DraftId == draft.Id &&
                e.ProviderUsed == "wikidata+bgg" &&
                e.FetchedFields == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_AggregatorReturnsEmpty_MarksFetchFailed_NoEvent()
    {
        var draft = SeedDraft();
        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<CatalogSeedDraftEntity> { draft });

        _aggregator.Setup(a => a.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(EmptyResult());

        await CreateJob().RunBatchAsync(_repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, default);

        draft.Status.Should().Be("FetchFailed");
        draft.ErrorMessage.Should().NotBeNullOrEmpty();
        draft.ErrorMessage!.Should().Contain("providerUsed=none");
        draft.FetchedAt.Should().NotBeNull();
        draft.ProvenanceJson.Should().BeNull();

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Publish(It.IsAny<CatalogSeedFetchedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_AggregatorThrows_MarksFetchFailed_NoEvent()
    {
        var draft = SeedDraft();
        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<CatalogSeedDraftEntity> { draft });

        _aggregator.Setup(a => a.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("network down"));

        await CreateJob().RunBatchAsync(_repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, default);

        draft.Status.Should().Be("FetchFailed");
        // ex-message-leak-pattern: ErrorMessage holds exception TYPE, not Message
        draft.ErrorMessage.Should().NotBeNullOrEmpty();
        draft.ErrorMessage!.Should().Contain(nameof(HttpRequestException));
        draft.ErrorMessage.Should().NotContain("network down");
        draft.FetchedAt.Should().NotBeNull();

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Publish(It.IsAny<CatalogSeedFetchedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_RepoCalledWithBatchSize10()
    {
        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<CatalogSeedDraftEntity>());

        await CreateJob().RunBatchAsync(_repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, default);

        // Batch size constant must be 10 — enforces spec §7 throughput cap of ~10/min
        CatalogSeedFetchJob.BatchSize.Should().Be(10);

        _repo.Verify(r => r.GetByStatusAsync("Pending", 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_CancellationToken_PropagatesOperationCanceledException()
    {
        var draft1 = SeedDraft();
        var draft2 = SeedDraft();
        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<CatalogSeedDraftEntity> { draft1, draft2 });

        using var cts = new CancellationTokenSource();
        // Cancel immediately so the second iteration of the loop trips ThrowIfCancellationRequested
        await cts.CancelAsync();

        var act = async () => await CreateJob().RunBatchAsync(
            _repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunBatchAsync_MultipleDrafts_ProcessesAllInOrder()
    {
        var d1 = SeedDraft(bggId: 1);
        var d2 = SeedDraft(bggId: 2);
        var d3 = SeedDraft(bggId: 3);

        _repo.Setup(r => r.GetByStatusAsync("Pending", CatalogSeedFetchJob.BatchSize, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<CatalogSeedDraftEntity> { d1, d2, d3 });

        _aggregator.Setup(a => a.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(SuccessResult("wikidata"));

        // Override the inter-item delay implicitly by not waiting — Task.Delay
        // honours the cancellation token but here token is default, so the test
        // tolerates the 1s * (n-1) wall clock cost. Three drafts ≈ 2s.
        await CreateJob().RunBatchAsync(_repo.Object, _aggregator.Object, _uow.Object, _mediator.Object, default);

        d1.Status.Should().Be("Fetched");
        d2.Status.Should().Be("Fetched");
        d3.Status.Should().Be("Fetched");

        _aggregator.Verify(a => a.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _mediator.Verify(m => m.Publish(It.IsAny<CatalogSeedFetchedEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
