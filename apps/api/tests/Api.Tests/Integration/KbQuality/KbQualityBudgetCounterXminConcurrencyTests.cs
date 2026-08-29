using Api.BoundedContexts.KbQuality.Application.Configuration;
using Api.BoundedContexts.KbQuality.Domain.Budget;
using Api.BoundedContexts.KbQuality.Infrastructure;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KbQuality;

/// <summary>
/// Concorrenza ottimistica su <c>kb_quality_budget_counters</c> (#3651, lotto 9).
///
/// <para>
/// <b>Questa entità è il caso in cui il difetto ha la conseguenza più concreta.</b> Il contatore
/// tiene la spesa mensile per tenant, e <c>EvaluationRepository.IncrementSpentAsync</c> è scritto
/// con un <b>retry loop esplicito</b> su <see cref="DbUpdateConcurrencyException"/> (3 tentativi,
/// detach e rilettura). Il commento dichiara: <i>«The RowVersion column (xmin) makes the conflict
/// observable»</i>.
/// </para>
/// <para>
/// Non era vero: il token era <c>byte[]</c> su una colonna <c>bytea</c> che Postgres non popola,
/// quindi quell'eccezione <b>non poteva essere sollevata</b> e il retry non è mai entrato in
/// funzione. Il codice difensivo esisteva, era corretto, e non veniva mai eseguito — mentre due
/// valutazioni concorrenti dello stesso tenant si sovrascrivevano l'incremento. Su un contatore di
/// spesa questo significa un tetto di budget superabile senza che nulla lo rilevi.
/// </para>
/// <para>
/// Per questo il secondo test non verifica il token ma il <b>comportamento che il token abilita</b>:
/// che il retry assorba il conflitto e nessun incremento vada perso.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KbQuality")]
[Trait("Issue", "3651")]
public sealed class KbQualityBudgetCounterXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public KbQualityBudgetCounterXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"kbbudget_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static EvaluationRepository CreateRepository(MeepleAiDbContext dbContext)
    {
        var options = new Mock<IOptionsMonitor<EvalQualityOptions>>();
        options.Setup(o => o.CurrentValue).Returns(new EvalQualityOptions());
        return new EvaluationRepository(dbContext, options.Object);
    }

    private static string CurrentYearMonth() => DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<Guid> SeedCounterAsync(decimal initialSpent)
    {
        var tenantId = Guid.NewGuid();
        _dbContext.KbQualityBudgetCounters.Add(
            KbQualityBudgetCounter.Create(tenantId, CurrentYearMonth(), initialSpent));
        await _dbContext.SaveChangesAsync();
        return tenantId;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var tenantId = await SeedCounterAsync(10m);
        var yearMonth = CurrentYearMonth();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        // Issue #3866: `.AsTracking()` is REQUIRED here. The DbContext default is NoTracking
        // (PERF-06), so a plain read hands back a DETACHED entity: the mutations below would reach
        // no change tracker, SaveChangesAsync would write nothing, and the concurrency token this
        // test exists to exercise would never even be compared. This is the documented opt-out for
        // a fixture whose subject IS a tracked read-modify-write.
        var seenByFirst = await firstContext.KbQualityBudgetCounters
            .AsTracking()
            .FirstAsync(c => c.TenantId == tenantId && c.YearMonth == yearMonth);
        var seenBySecond = await secondContext.KbQualityBudgetCounters
            .AsTracking()
            .FirstAsync(c => c.TenantId == tenantId && c.YearMonth == yearMonth);

        seenByFirst.IncrementSpent(15m);
        await firstContext.SaveChangesAsync();

        seenBySecond.IncrementSpent(5m);

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "è il conflitto che il retry loop di IncrementSpentAsync dichiara di gestire: senza "
            + "che venga sollevato, quel codice non viene mai eseguito e un incremento di spesa "
            + "sovrascrive l'altro (#3651)");
    }

    [Fact]
    public async Task IncrementSpentAsync_WithStaleTrackedCounter_RetriesAndKeepsBothIncrements()
    {
        var tenantId = await SeedCounterAsync(10m);
        var yearMonth = CurrentYearMonth();

        await using var slowContext = _fixture.CreateDbContext(_connectionString);
        var slowRepository = CreateRepository(slowContext);

        // Il contesto "lento" traccia la riga a 10: da qui in poi la sua copia è destinata a
        // diventare stale, ed è la condizione che il retry deve saper riconoscere.
        await slowContext.KbQualityBudgetCounters
            .AsTracking() // #3866: senza tracking non esiste la copia stale che il retry deve riconoscere
            .FirstAsync(c => c.TenantId == tenantId && c.YearMonth == yearMonth);

        await using (var fastContext = _fixture.CreateDbContext(_connectionString))
        {
            await CreateRepository(fastContext).IncrementSpentAsync(tenantId, 15m, CancellationToken.None);
        }

        // Ora il contesto lento incrementa partendo da una versione superata. Con il token attivo
        // SaveChanges solleva, il loop fa detach, rilegge 25 e riapplica 5 → 30.
        await slowRepository.IncrementSpentAsync(tenantId, 5m, CancellationToken.None);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.KbQualityBudgetCounters
            .FirstAsync(c => c.TenantId == tenantId && c.YearMonth == yearMonth);

        persisted.SpentUsd.Should().Be(30m,
            "nessun incremento deve andare perso: 10 iniziali + 15 + 5. Con il token inerte il "
            + "secondo scrittore sovrascriveva con 15, e il tetto di budget diventava superabile "
            + "senza che nulla lo segnalasse");
    }
}
