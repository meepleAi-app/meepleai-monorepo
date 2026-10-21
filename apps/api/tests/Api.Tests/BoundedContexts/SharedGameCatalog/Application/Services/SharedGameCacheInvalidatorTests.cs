using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services;
using Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4603 — <see cref="SharedGameCacheInvalidator"/>, l'unico punto in cui un comando toglie dalla cache il
/// dettaglio e la lista del catalogo.
/// </summary>
/// <remarks>
/// Fissa quattro cose, perché sono quelle da cui dipendono gli handler e che un cambio silenzioso
/// romperebbe: i tag (e che il dettaglio sia per gioco), i due passi per tag nell'ordine wrapper e poi
/// <see cref="HybridCache"/> nativo, il comportamento in errore (rilancio dopo i tentativi della retry
/// policy: la questione aperta di #4603 punto 2) e il token della richiesta. La retry policy dei doppi
/// ESEGUE il delegato, come quella vera, altrimenti il doppio della cache non viene esercitato.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameCacheInvalidatorTests
{
    private const string ListTag = "search-games";

    private readonly DetailCacheEvictionDoubles _doubles = new();

    private static string DetailTag(Guid gameId) => $"shared-game:{gameId}";

    [Fact]
    public async Task EvictDetail_TouchesOnlyTheDetailTagOfThatGame_WrapperThenNative()
    {
        var gameId = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailAsync(gameId, TestContext.Current.CancellationToken);

        _doubles.Events.Should().Equal(
            new[] { $"wrapper:{DetailTag(gameId)}", $"native:{DetailTag(gameId)}" },
            "errata and FAQ change the detail and nothing the list shows: the list tag stays");
    }

    [Fact]
    public async Task EvictDetailAndList_EvictsTheListFirst_ThenTheDetailOfThatGame()
    {
        var gameId = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailAndListAsync(gameId, TestContext.Current.CancellationToken);

        _doubles.Events.Should().Equal(
            new[]
            {
                $"wrapper:{ListTag}", $"native:{ListTag}",
                $"wrapper:{DetailTag(gameId)}", $"native:{DetailTag(gameId)}",
            },
            "every tag gets both steps, the wrapper first: the native one is what reaches an entry written "
            + "with the raw HybridCache on this replica, with or without Redis (#4561)");
    }

    [Fact]
    public async Task EvictDetailsAndList_ForManyGames_EvictsTheListOnce_AndEachDetailOnce()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailsAndListAsync([first, second], TestContext.Current.CancellationToken);

        _doubles.Events.Should().Equal(
            new[]
            {
                $"wrapper:{ListTag}", $"native:{ListTag}",
                $"wrapper:{DetailTag(first)}", $"native:{DetailTag(first)}",
                $"wrapper:{DetailTag(second)}", $"native:{DetailTag(second)}",
            });
    }

    [Fact]
    public async Task EvictDetailsAndList_ForNoGames_DoesNothing_ListIncluded()
    {
        await _doubles.Invalidator.EvictDetailsAndListAsync([], TestContext.Current.CancellationToken);

        _doubles.Events.Should().BeEmpty("a bulk action that changed no game has nothing to evict");
    }

    [Fact]
    public async Task Eviction_UsesTheRetryPolicy_WithTheMetricNamesOfTheListAndOfTheDetail()
    {
        var names = new List<string>();
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, name, ct) =>
            {
                names.Add(name);
                return op(ct).AsTask();
            });

        await _doubles.Invalidator.EvictDetailAndListAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        names.Should().Equal(
            "shared-games.list", "shared-games.list", "shared-games.detail", "shared-games.detail");
    }

    /// <summary>
    /// Il comportamento di oggi, fissato perché il punto 2 di #4603 non è deciso: un'evizione che fallisce
    /// dopo i tentativi risale al chiamante, e il passo nativo non parte.
    /// </summary>
    [Fact]
    public async Task WhenTheRetryPolicyGivesUp_TheErrorReachesTheCaller_AndTheNativeStepDoesNotRun()
    {
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var act = () => _doubles.Invalidator.EvictDetailAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("redis down");
        _doubles.HybridCache.Verify(
            c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Eviction_PassesTheRequestTokenToEveryStep()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;
        var gameId = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailAndListAsync(gameId, token);

        _doubles.RetryPolicy.Verify(
            p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), token),
            Times.Exactly(4));
        _doubles.Cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(ListTag, token), Times.Once);
        _doubles.Cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(DetailTag(gameId), token), Times.Once);
        _doubles.HybridCache.Verify(c => c.RemoveByTagAsync(ListTag, token), Times.Once);
        _doubles.HybridCache.Verify(c => c.RemoveByTagAsync(DetailTag(gameId), token), Times.Once);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var cache = new Mock<IHybridCacheService>().Object;
        var retry = new Mock<ICacheInvalidationRetryPolicy>().Object;
        var hybrid = new Mock<HybridCache>().Object;

        FluentActions.Invoking(() => new SharedGameCacheInvalidator(null!, retry, hybrid))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SharedGameCacheInvalidator(cache, null!, hybrid))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SharedGameCacheInvalidator(cache, retry, null!))
            .Should().Throw<ArgumentNullException>();
    }
}
