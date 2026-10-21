using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// #4557 — il voto è un incremento sulla riga della FAQ: l'handler non carica né salva l'aggregato
/// <c>SharedGame</c>. Che l'incremento non tocchi la riga del gioco lo prova
/// <c>SharedGameCatalogEndpointsIntegrationTests.UpvoteFaq_DoesNotWriteTheGameRow</c>, su Postgres.
/// </summary>
/// <remarks>
/// #4565 — dopo l'incremento il dettaglio del gioco in cache (<c>shared-game:{id}</c>) si evince, con il
/// gioco che il repository restituisce insieme al contatore. La prova end-to-end è
/// <c>Detail_AfterAnUpvote_ReportsTheNewCount</c>.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpvoteFaqCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly DetailCacheEvictionDoubles _eviction = new();
    private readonly UpvoteFaqCommandHandler _handler;

    public UpvoteFaqCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>(MockBehavior.Strict);
        _handler = new UpvoteFaqCommandHandler(
            _repositoryMock.Object,
            _eviction.Invalidator,
            new Mock<ILogger<UpvoteFaqCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_WithValidFaq_ReturnsTheNewCount()
    {
        var faqId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FaqUpvoteResult(Guid.NewGuid(), 4));

        var result = await _handler.Handle(new UpvoteFaqCommand(faqId), TestContext.Current.CancellationToken);

        result.Id.Should().Be(faqId);
        result.UpvoteCount.Should().Be(4);
        _repositoryMock.Verify(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotLoadOrUpdateTheAggregate()
    {
        // Strict: qualunque chiamata diversa dall'incremento (GetGameByFaqIdAsync, Update) fa fallire.
        var faqId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FaqUpvoteResult(Guid.NewGuid(), 1));

        await _handler.Handle(new UpvoteFaqCommand(faqId), TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithNonExistentFaq_ThrowsNotFoundException()
    {
        var faqId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FaqUpvoteResult?)null);

        var act = () => _handler.Handle(new UpvoteFaqCommand(faqId), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AfterTheIncrement_EvictsTheDetailCacheOfTheFaqsGame()
    {
        // Il comando porta solo l'id della FAQ: il gioco da evincere è quello che il repository
        // restituisce con il contatore.
        var faqId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()))
            .Callback(_eviction.MarkWritten)
            .ReturnsAsync(new FaqUpvoteResult(gameId, 2));

        await _handler.Handle(new UpvoteFaqCommand(faqId), TestContext.Current.CancellationToken);

        _eviction.AssertEvictedAfterTheWrite(gameId);
    }

    [Fact]
    public async Task Handle_WithNonExistentFaq_EvictsNothing()
    {
        var faqId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.IncrementFaqUpvoteAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FaqUpvoteResult?)null);

        var act = () => _handler.Handle(new UpvoteFaqCommand(faqId), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _eviction.AssertNothingEvicted();
    }
}
