using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.GameToolkit.Application.EventHandlers;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler"/> (ADR-069 #2383).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public sealed class InvalidateToolkitSuggestionCacheOnKbDocIndexedHandlerTests
{
    [Fact]
    public async Task Handle_KbDocIndexedWithGameId_DeletesCacheForGameId()
    {
        var gameId = Guid.NewGuid();
        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.DeleteByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

        var sut = new InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
            cacheRepo.Object,
            NullLogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler>.Instance);

        var evt = new KbDocIndexedEvent(
            aggregateId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            fileName: "catan-rules.pdf",
            gameId: gameId,
            gameName: null,
            pageCount: 42);

        await sut.Handle(evt, CancellationToken.None);

        cacheRepo.Verify();
    }

    [Fact]
    public async Task Handle_KbDocIndexedWithNullGameId_SkipsDeleteAndLogs()
    {
        // GameId is nullable — private-game documents have no SharedGameId
        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>(MockBehavior.Strict);
        // Strict: any unexpected call will throw

        var sut = new InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
            cacheRepo.Object,
            NullLogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler>.Instance);

        var evt = new KbDocIndexedEvent(
            aggregateId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            fileName: "private-rules.pdf",
            gameId: null,  // private game document — no SharedGameId
            gameName: null,
            pageCount: null);

        // Act — must NOT throw and must NOT call DeleteByGameIdAsync
        await sut.Handle(evt, CancellationToken.None);

        cacheRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_DeleteThrows_LogsAndDoesNotRethrow()
    {
        var gameId = Guid.NewGuid();
        var cacheRepo = new Mock<IAiToolkitSuggestionCacheRepository>();
        cacheRepo.Setup(r => r.DeleteByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("db down"));

        var sut = new InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler(
            cacheRepo.Object,
            NullLogger<InvalidateToolkitSuggestionCacheOnKbDocIndexedHandler>.Instance);

        var act = async () => await sut.Handle(
            new KbDocIndexedEvent(Guid.NewGuid(), Guid.NewGuid(), "rules.pdf", gameId, null, null),
            CancellationToken.None);

        // Must NOT throw — cache failure must not roll back the indexing pipeline
        await act.Should().NotThrowAsync();
    }
}
