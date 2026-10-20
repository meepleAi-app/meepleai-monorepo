using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Moq;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// I doppi per verificare l'evizione del dettaglio in cache (#4565) negli handler di errata e FAQ,
/// con l'ordine rispetto alla scrittura.
/// </summary>
/// <remarks>
/// <para>
/// La retry policy ESEGUE il delegato (come in <c>SetRagPublicAccessCommandHandlerTests</c> e in
/// <c>UpdateSharedGameCommandHandlerTests</c>, #4100): con un mock che non lo chiama il doppio della
/// cache non viene esercitato, e un test sull'evizione passa senza che nulla sia evinto.
/// </para>
/// <para>
/// <see cref="Events"/> registra in ordine la scrittura (<see cref="MarkWritten"/>, da chiamare dal
/// callback dello unit of work o del repository) e le due evizioni. L'evizione prima della scrittura
/// lascerebbe che una lettura ripopoli la cache col contenuto vecchio.
/// </para>
/// </remarks>
internal sealed class DetailCacheEvictionDoubles
{
    public const string Written = "written";

    public Mock<IHybridCacheService> Cache { get; } = new();

    public Mock<ICacheInvalidationRetryPolicy> RetryPolicy { get; } = new();

    public Mock<HybridCache> HybridCache { get; } = new();

    /// <summary>La scrittura e le evizioni, nell'ordine in cui sono avvenute.</summary>
    public List<string> Events { get; } = [];

    public DetailCacheEvictionDoubles()
    {
        Cache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((tag, _) => Events.Add($"wrapper:{tag}"))
            .ReturnsAsync(0);

        RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) => op(ct).AsTask());

        HybridCache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((tag, _) => Events.Add($"native:{tag}"))
            .Returns(ValueTask.CompletedTask);
    }

    public void MarkWritten() => Events.Add(Written);

    /// <summary>
    /// Dopo la scrittura, il tag di dettaglio di QUEL gioco è stato tolto con entrambi i passi, e
    /// nient'altro: né la lista (<c>search-games</c>) né il dettaglio di un altro gioco.
    /// </summary>
    public void AssertEvictedAfterTheWrite(Guid gameId)
    {
        var tag = $"shared-game:{gameId}";

        Events.Should().Equal(
            new[] { Written, $"wrapper:{tag}", $"native:{tag}" },
            "the detail is evicted once, after the write, through the wrapper (retry + other replicas) "
            + "and through the native HybridCache that the detail is written with, which is what makes "
            + "the next read on this replica miss, with or without Redis");
    }

    public void AssertNothingEvicted() =>
        Events.Where(e => e != Written).Should().BeEmpty("a failed command must not evict anything");

    public static SharedGame NewGame() => SharedGame.Create(
        title: "Test Game",
        yearPublished: 2020,
        description: "Test",
        minPlayers: 2,
        maxPlayers: 4,
        playingTimeMinutes: 60,
        minAge: 10,
        complexityRating: null,
        averageRating: null,
        imageUrl: "https://example.com/game.jpg",
        thumbnailUrl: "https://example.com/thumb.jpg",
        rules: null,
        createdBy: Guid.NewGuid(),
        bggId: null);
}
