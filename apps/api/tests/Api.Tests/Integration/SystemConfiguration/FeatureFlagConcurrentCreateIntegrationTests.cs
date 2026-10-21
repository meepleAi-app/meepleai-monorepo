using Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perché e le condizioni per
/// condividere il database stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perché condividere il database è sicuro QUI.</b> Ogni test lavora su una chiave sua: un nome di
/// flag con un GUID, oppure — per i test del comando di toggle, che accetta solo le chiavi di
/// <c>FeatureFlagConstants.RagEnhancements</c> — una chiave diversa per test. Le asserzioni leggono solo
/// le righe di quella chiave (e delle sue varianti per tier). Nessun conteggio e nessuna lista completa.
/// </para>
/// </summary>
public sealed class FeatureFlagConcurrentCreateHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "ff_concurrent_create")
{
    /// <summary>
    /// Le gare sul primo toggle (#4599): il servizio di configurazione è decorato perché un test possa
    /// fermare una richiesta dopo la ricerca della chiave. Senza un'azione registrata il decoratore è
    /// trasparente, quindi gli altri test della classe non lo vedono.
    /// </summary>
    internal ConfigurationLookupInterleaving Lookups { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(Lookups.Decorate));
}

/// <summary>
/// Il primo toggle concorrente di un flag che non ha ancora una riga (#4599).
/// </summary>
/// <remarks>
/// <para>
/// Due richieste su una chiave senza riga leggono entrambe «nessuna riga» ed eseguono entrambe
/// <c>CreateConfigurationCommand</c>. L'indice univoco su <c>(Key, Environment)</c> fa fallire la seconda
/// con <c>DbUpdateException</c> / 23505, che il middleware non mappa: 500. In produzione l'ordine lo decide
/// il caso; qui è fisso. <see cref="ConfigurationLookupInterleaving"/> ferma la prima richiesta subito
/// dopo la ricerca e, in quel punto, esegue per intero la seconda, in un altro scope e quindi con un altro
/// <c>DbContext</c>: la seconda crea la riga, la prima ci arriva dopo e la trova già lì.
/// </para>
/// <para>
/// Il database è vero (indice univoco compreso) e il servizio di cache dell'host non memorizza niente
/// (<c>TestHybridCacheService</c>): la gara qui è solo quella del database. L'invalidazione della cache
/// la prova <c>FeatureFlagServiceCacheInvalidationTests</c>.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4599")]
public sealed class FeatureFlagConcurrentCreateIntegrationTests : IClassFixture<FeatureFlagConcurrentCreateHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ConfigurationLookupInterleaving _lookups;

    public FeatureFlagConcurrentCreateIntegrationTests(FeatureFlagConcurrentCreateHostFixture host)
    {
        _factory = host.Factory;
        _lookups = host.Lookups;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ========================================
    // I QUATTRO PERCORSI DI CREAZIONE DI FeatureFlagService
    // ========================================

    [Fact]
    public async Task EnableFeature_AnotherRequestCreatedTheRowAfterTheLookup_UpdatesInsteadOfFailing()
    {
        var flag = NewFlagName();
        var admin = await CreateUserAsync();

        var rows = await RaceAsync(
            flag,
            loser: ff => ff.EnableFeatureAsync(flag, userId: admin.ToString()),
            winner: ff => ff.EnableFeatureAsync(flag, userId: admin.ToString()));

        rows.Should().ContainSingle("the loser must update the row the winner created, not create a second one")
            .Which.Value.Should().Be("true");
    }

    [Fact]
    public async Task EnableFeature_AnotherRequestCreatedTheRowAfterTheLookup_LeavesNoTraceOfTheFailedCreation()
    {
        // The create that lost the race never happened, so the outbox — which feeds the audit log — must hold
        // the same events as two requests that ran one after the other. A failed save leaves its own outbox
        // row and its pending event in the context that ran it; the loser must not reuse that context.
        //
        // The comparison is with a sequential run, not with fixed counts: loading a configuration to update
        // it also raises a ConfigurationCreatedEvent (the aggregate constructor adds it), so an update is
        // always [created, updated] in the outbox. That is how it was before #4599 and is not touched here.
        var flag = NewFlagName();
        var sequentialFlag = NewFlagName();
        var admin = await CreateUserAsync();

        await RunAsync(ff => ff.EnableFeatureAsync(sequentialFlag, userId: admin.ToString()));
        await RunAsync(ff => ff.EnableFeatureAsync(sequentialFlag, userId: admin.ToString()));
        var sequential = await OutboxEventTypesAsync(sequentialFlag);
        sequential.Should().Contain(t => t.EndsWith(nameof(ConfigurationUpdatedEvent), StringComparison.Ordinal),
            "setup: the sequential run must have produced the events to compare with");

        await RaceAsync(
            flag,
            loser: ff => ff.EnableFeatureAsync(flag, userId: admin.ToString()),
            winner: ff => ff.EnableFeatureAsync(flag, userId: admin.ToString()));

        (await OutboxEventTypesAsync(flag)).Should().BeEquivalentTo(sequential,
            "a creation that lost the race must leave no event behind: it never created anything");
    }

    [Fact]
    public async Task DisableFeature_AnotherRequestCreatedTheRowAfterTheLookup_ReachesTheValueItAskedFor()
    {
        var flag = NewFlagName();
        var admin = await CreateUserAsync();

        // The winner creates the row as "true"; the loser wanted "false" and must leave it "false", as if it
        // had run after the winner.
        var rows = await RaceAsync(
            flag,
            loser: ff => ff.DisableFeatureAsync(flag, userId: admin.ToString()),
            winner: ff => ff.EnableFeatureAsync(flag, userId: admin.ToString()));

        rows.Should().ContainSingle().Which.Value.Should().Be("false",
            "the request that lost the race applies its own value on top of the winner's, like a request that arrived after it");
    }

    [Fact]
    public async Task EnableFeatureForTier_AnotherRequestCreatedTheRowAfterTheLookup_UpdatesInsteadOfFailing()
    {
        var flag = NewFlagName();
        var tierKey = $"{flag}.Tier.{UserTier.Premium.Value}";
        var admin = await CreateUserAsync();

        var rows = await RaceAsync(
            tierKey,
            loser: ff => ff.EnableFeatureForTierAsync(flag, UserTier.Premium, admin.ToString()),
            winner: ff => ff.EnableFeatureForTierAsync(flag, UserTier.Premium, admin.ToString()));

        rows.Should().ContainSingle().Which.Value.Should().Be("true");
    }

    [Fact]
    public async Task DisableFeatureForTier_AnotherRequestCreatedTheRowAfterTheLookup_ReachesTheValueItAskedFor()
    {
        var flag = NewFlagName();
        var tierKey = $"{flag}.Tier.{UserTier.Premium.Value}";
        var admin = await CreateUserAsync();

        var rows = await RaceAsync(
            tierKey,
            loser: ff => ff.DisableFeatureForTierAsync(flag, UserTier.Premium, admin.ToString()),
            winner: ff => ff.EnableFeatureForTierAsync(flag, UserTier.Premium, admin.ToString()));

        rows.Should().ContainSingle().Which.Value.Should().Be("false");
    }

    // ========================================
    // IL COMANDO DI TOGGLE (la riproduzione di #4599)
    // ========================================

    [Fact]
    public async Task ToggleRagEnhancement_TwoFirstTogglesOfAKeyWithoutRow_BothSucceedAndTheFlagEndsOn()
    {
        var key = FeatureFlagConstants.RagEnhancements[0];
        var admin = await CreateUserAsync();
        var winnerResult = (bool?)null;

        _lookups.AfterNextLookup(key, async () => winnerResult = await SendAsync(new ToggleRagEnhancementCommand(key, admin)));
        var loserResult = await SendAsync(new ToggleRagEnhancementCommand(key, admin));

        using (new AssertionScope())
        {
            _lookups.IsPending(key).Should().BeFalse("setup: the second toggle must run inside the first");
            winnerResult.Should().BeTrue("setup: the first toggle of a key without row turns it on");
            loserResult.Should().BeTrue(
                "both toggles read «off» and asked for «on»: the one that lost the race must answer like the one that won, not 500");
            (await RowsAsync(key)).Should().ContainSingle().Which.Value.Should().Be("true");
        }
    }

    [Fact]
    public async Task ToggleRagEnhancement_TwoFirstTogglesOfATierWithoutRow_BothSucceedAndTheTierEndsOff()
    {
        var key = FeatureFlagConstants.RagEnhancements[1];
        var tierKey = $"{key}.Tier.{UserTier.Free.Value}";
        var admin = await CreateUserAsync();
        var winnerResult = (bool?)null;

        _lookups.AfterNextLookup(tierKey, async () => winnerResult = await SendAsync(new ToggleRagEnhancementCommand(key, admin, UserTier.Free)));
        var loserResult = await SendAsync(new ToggleRagEnhancementCommand(key, admin, UserTier.Free));

        using (new AssertionScope())
        {
            _lookups.IsPending(tierKey).Should().BeFalse("setup: the second toggle must run inside the first");
            // A tier without row follows the global flag and, with none, is «on»: the first toggle turns it off.
            winnerResult.Should().BeFalse("setup: the first toggle of a tier without row turns it off");
            loserResult.Should().BeFalse("both toggles read «on» and asked for «off»");
            (await RowsAsync(tierKey)).Should().ContainSingle().Which.Value.Should().Be("false");
        }
    }

    // ========================================
    // QUANDO IL RIMEDIO NON PUÒ AIUTARE
    // ========================================

    [Fact]
    public async Task EnableFeature_UniqueViolationOnARowTheLookupCannotSee_StillFails()
    {
        // 🔴 This test photographs a KNOWN LIMIT, not a contract: today this case answers 500, and it should not.
        // It is tracked in #4607 (https://github.com/meepleAi-app/meepleai-monorepo/issues/4607); when that is
        // fixed, change the expectation here instead of defending the 500.
        //
        // The lookup only sees active rows, so an inactive row for the same (Key, Environment) is invisible to
        // it and still trips the unique index. Re-reading finds nothing to update: what #4599 guarantees is that
        // the original error comes out, not a silent success and not a second attempt.
        var flag = NewFlagName();
        var admin = await CreateUserAsync();
        await InsertInactiveRowAsync(flag, admin);

        var act = async () => await RunAsync(ff => ff.EnableFeatureAsync(flag, userId: admin.ToString()));

        var failure = await act.Should().ThrowAsync<DbUpdateException>();
        failure.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await RowsAsync(flag)).Should().ContainSingle().Which.IsActive.Should().BeFalse("the inactive row is left as it was");
    }

    // ========================================
    // SUPPORTO
    // ========================================

    private static string NewFlagName() => $"test.concurrent-create.{Guid.NewGuid():N}";

    /// <summary>
    /// Ferma <paramref name="loser"/> dopo la ricerca di <paramref name="lookupKey"/>, esegue
    /// <paramref name="winner"/> per intero in quel punto, e restituisce le righe di quella chiave.
    /// </summary>
    private async Task<List<SystemConfigurationEntity>> RaceAsync(
        string lookupKey,
        Func<IFeatureFlagService, Task> loser,
        Func<IFeatureFlagService, Task> winner)
    {
        _lookups.AfterNextLookup(lookupKey, () => RunAsync(winner));

        await RunAsync(loser);

        _lookups.IsPending(lookupKey).Should().BeFalse("setup: the winner must run between the loser's lookup and its create");
        return await RowsAsync(lookupKey);
    }

    private async Task RunAsync(Func<IFeatureFlagService, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<IFeatureFlagService>());
    }

    private async Task<bool> SendAsync(ToggleRagEnhancementCommand command)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, Ct);
    }

    private async Task<Guid> CreateUserAsync()
    {
        // The author of a configuration row is a foreign key to users.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        return userId;
    }

    private async Task<List<SystemConfigurationEntity>> RowsAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<SystemConfigurationEntity>().AsNoTracking().Where(c => c.Key == key).ToListAsync(Ct);
    }

    /// <summary>Tipo degli eventi di dominio in outbox che parlano di <paramref name="key"/>.</summary>
    private async Task<List<string>> OutboxEventTypesAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        // The payload is jsonb, which LIKE does not accept: the filter on the key runs in memory. The table
        // only holds what the tests of this class wrote.
        var rows = await db.DomainEventOutbox.AsNoTracking()
            .Select(o => new { o.EventType, o.PayloadJson })
            .ToListAsync(Ct);
        return rows.Where(o => o.PayloadJson.Contains(key, StringComparison.Ordinal)).Select(o => o.EventType).ToList();
    }

    private async Task InsertInactiveRowAsync(string key, Guid author)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = DateTime.UtcNow;
        db.Set<SystemConfigurationEntity>().Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = "false",
            ValueType = "Boolean",
            Category = "FeatureFlags",
            IsActive = false,
            Environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().EnvironmentName,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = author,
        });
        await db.SaveChangesAsync(Ct);
    }
}
