using Api.BoundedContexts.KbQuality.Domain.Budget;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.KbQuality;

/// <summary>
/// Concorrenza ottimistica su <c>kb_quality_budget_counters</c> (#3651, lotto 9).
///
/// <para>
/// <b>Questa entità è il caso in cui il difetto ha la conseguenza più concreta.</b> Il contatore
/// teneva la spesa mensile per tenant, e <c>EvaluationRepository.IncrementSpentAsync</c> era scritto
/// con un <b>retry loop esplicito</b> su <see cref="DbUpdateConcurrencyException"/> (3 tentativi,
/// detach e rilettura). Il commento dichiarava: <i>«The RowVersion column (xmin) makes the conflict
/// observable»</i>.
/// </para>
/// <para>
/// Non era vero: il token era <c>byte[]</c> su una colonna <c>bytea</c> che Postgres non popola,
/// quindi quell'eccezione <b>non poteva essere sollevata</b> e il retry non è mai entrato in
/// funzione. Il codice difensivo esisteva, era corretto, e non veniva mai eseguito, mentre due
/// valutazioni concorrenti dello stesso tenant si sovrascrivevano l'incremento. Su un contatore di
/// spesa questo significa un tetto di budget superabile senza che nulla lo rilevi.
/// </para>
/// <para>
/// #4166 (ADR-095 D6): il contatore non si legge né si scrive più, perché il tetto di KbQuality è
/// diventato la quota delle valutazioni dentro il tetto unico del gateway LLM. Con lui sono usciti
/// <c>IncrementSpentAsync</c>, il suo retry loop e il test che lo esercitava. Resta questo test del
/// token: finché la tabella esiste, fino alla consegna che la toglie, l'entità deve rendere visibile
/// il conflitto.
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
            "senza il conflitto un incremento di spesa sovrascrive l'altro in silenzio (#3651)");
    }
}
