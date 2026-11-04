using System.Text.Json;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AuthUser = Api.BoundedContexts.Authentication.Domain.Entities.User;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// #3845 / #3839 — cio' che finisce nella cache dev'essere rileggibile.
///
/// <c>RateLimitEvaluator</c> metteva in cache l'aggregato <c>ShareRequestLimitConfig</c>. Scriverlo
/// funzionava; rileggerlo no: l'entita' segue la convenzione DDD del progetto (costruttori privati
/// piu' factory) e System.Text.Json — il serializzatore di HybridCache — rifiuta di deserializzare
/// un tipo senza costruttore pubblico utilizzabile.
///
/// Il difetto era <b>differito</b>, ed e' questo che lo rendeva difficile da vedere: la prima
/// richiesta popolava la cache e rispondeva 200, tutte le successive rispondevano 500 finche' la
/// voce non scadeva (15 minuti). Un test che chiama l'handler una volta sola non lo vede.
///
/// Per questo la cache finta qui <b>serializza davvero</b> e chiama l'evaluator <b>due volte</b>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class RateLimitEvaluatorCacheRoundTripTests
{
    /// <summary>
    /// Cache che si comporta come quella vera: quello che entra viene serializzato con
    /// System.Text.Json, e alla lettura successiva viene deserializzato. Una cache che tenesse
    /// l'oggetto in memoria non riprodurrebbe il difetto.
    /// </summary>
    private sealed class CacheCheSerializza : IHybridCacheService
    {
        private readonly Dictionary<string, string> _voci = new(StringComparer.Ordinal);

        public async Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            string[]? tags = null,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class
        {
            if (_voci.TryGetValue(cacheKey, out var json))
            {
                return JsonSerializer.Deserialize<T>(json)!;
            }

            var valore = await factory(ct).ConfigureAwait(false);
            _voci[cacheKey] = JsonSerializer.Serialize(valore);
            return valore;
        }

        public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
        {
            _voci.Remove(cacheKey);
            return Task.CompletedTask;
        }

        public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

        public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);

        public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

        public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException("non serve a questo test");
    }

    private static RateLimitEvaluator CostruisciEvaluator(Guid userId, out CacheCheSerializza cache)
    {
        // Un utente non-admin: il tier Admin bypassa la cache e il test non proverebbe nulla.
        var utente = AuthUser.CreateForOAuth(
            id: userId,
            email: new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email("prova@meepleai.test"),
            displayName: "Prova",
            role: Api.SharedKernel.Domain.ValueObjects.Role.User,
            tier: null,
            oauthProvider: "google",
            timeProvider: TimeProvider.System);

        var configRepo = new Mock<IRateLimitConfigRepository>();
        configRepo
            .Setup(r => r.GetByTierAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareRequestLimitConfig.Create(
                tier: UserTier.Free,
                maxPendingRequests: 3,
                maxRequestsPerMonth: 10,
                cooldownAfterRejection: TimeSpan.FromDays(7)));

        var overrideRepo = new Mock<IUserRateLimitOverrideRepository>();
        overrideRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRateLimitOverride?)null);

        var shareRepo = new Mock<IShareRequestRepository>();
        shareRepo.Setup(r => r.CountPendingByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        shareRepo.Setup(r => r.CountThisMonthByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        shareRepo.Setup(r => r.GetLastRejectionDateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(utente);

        cache = new CacheCheSerializza();

        return new RateLimitEvaluator(
            configRepo.Object,
            overrideRepo.Object,
            shareRepo.Object,
            userRepo.Object,
            cache,
            NullLogger<RateLimitEvaluator>.Instance);
    }

    [Fact]
    public async Task GetUserStatusAsync_ChiamatoDueVolte_RileggeLaVoceDiCacheSenzaEsplodere()
    {
        var userId = Guid.NewGuid();
        var evaluator = CostruisciEvaluator(userId, out _);

        var prima = await evaluator.GetUserStatusAsync(userId, TestContext.Current.CancellationToken);
        prima.Should().NotBeNull();

        // La seconda chiamata legge dalla cache: e' qui che il codice difettoso sollevava
        // NotSupportedException, e l'endpoint rispondeva 500 (#3845).
        var azione = async () => await evaluator.GetUserStatusAsync(userId, TestContext.Current.CancellationToken);

        await azione.Should().NotThrowAsync(
            "cio' che viene messo in cache dev'essere rileggibile: l'aggregato di dominio non lo e'");
    }

    [Fact]
    public async Task GetUserStatusAsync_DallaCache_ConservaILimitiDelTier()
    {
        var userId = Guid.NewGuid();
        var evaluator = CostruisciEvaluator(userId, out _);

        var prima = await evaluator.GetUserStatusAsync(userId, TestContext.Current.CancellationToken);
        var dopo = await evaluator.GetUserStatusAsync(userId, TestContext.Current.CancellationToken);

        // Non basta che non esploda: un round-trip che perdesse i valori darebbe limiti a zero,
        // cioe' un utente bloccato senza motivo.
        dopo.EffectiveMaxPending.Should().Be(prima.EffectiveMaxPending).And.Be(3);
        dopo.EffectiveMaxPerMonth.Should().Be(prima.EffectiveMaxPerMonth).And.Be(10);
        dopo.EffectiveCooldown.Should().Be(prima.EffectiveCooldown).And.Be(TimeSpan.FromDays(7));
    }
}
