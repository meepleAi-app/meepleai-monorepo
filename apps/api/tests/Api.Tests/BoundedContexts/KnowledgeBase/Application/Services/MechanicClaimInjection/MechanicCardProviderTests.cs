using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class MechanicCardProviderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IHybridCacheService> _cache = new();

    private MechanicCardProvider Build()
    {
        // Fake cache: always invoke the factory (no real caching) so provider logic is exercised.
        _cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<MechanicCardCacheEntry>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<MechanicCardCacheEntry>> factory, string[]? _, TimeSpan? _, CancellationToken ct)
                => factory(ct));
        return new MechanicCardProvider(_mediator.Object, _cache.Object, NullLogger<MechanicCardProvider>.Instance);
    }

    private static PublishedMechanicCardDto SampleCard(Guid gameId) => new(
        CardId: Guid.NewGuid(),
        SharedGameId: gameId,
        Title: "T",
        Version: 1,
        PublishedAt: DateTime.UtcNow,
        GameName: "G",
        Publisher: null,
        Language: "it",
        Sections: Array.Empty<PublishedMechanicCardSectionDto>(),
        SourceAnalysisId: Guid.NewGuid(),
        PublicationYear: null,
        DocumentName: null);

    [Fact]
    public async Task GetActiveCardAsync_ReturnsCard_WhenQueryReturnsOne()
    {
        var gameId = Guid.NewGuid();
        var card = SampleCard(gameId);
        _mediator.Setup(m => m.Send(It.IsAny<GetPublishedMechanicCardByGameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var result = await Build().GetActiveCardAsync(gameId, CancellationToken.None);

        result.Should().BeSameAs(card);
    }

    [Fact]
    public async Task GetActiveCardAsync_ReturnsNull_WhenQueryReturnsNull()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPublishedMechanicCardByGameQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedMechanicCardDto?)null);

        var result = await Build().GetActiveCardAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveCardAsync_ReturnsNull_WhenMediatorThrows_BestEffort()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPublishedMechanicCardByGameQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cross-BC boom"));

        var result = await Build().GetActiveCardAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveCardAsync_PropagatesCancellation_NotSwallowedAsFailOpen()
    {
        // Best-effort catch must still honour caller cancellation (repo rule): a genuine cancel must
        // NOT be logged as a failed read and converted into a fail-open "no card" result.
        _mediator.Setup(m => m.Send(It.IsAny<GetPublishedMechanicCardByGameQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => Build().GetActiveCardAsync(Guid.NewGuid(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetActiveCardAsync_ReturnsNull_ForEmptyGuid_WithoutQuerying()
    {
        var result = await Build().GetActiveCardAsync(Guid.Empty, CancellationToken.None);

        result.Should().BeNull();
        _mediator.Verify(
            m => m.Send(It.IsAny<GetPublishedMechanicCardByGameQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
