using Api.Infrastructure;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #3656 — inventario dei seed che un database creato <b>da zero</b> deve contenere.
///
/// <para>
/// <b>Perché esiste.</b> Il repository ha subito tre squash di migration (<c>eff570a08</c> #2021,
/// <c>7d9d67c13</c> #2880, <c>414b30230</c> #3518). Le stesse due righe di seed sono state perse
/// <b>due volte</b>: eliminate da #2021, ripristinate il 2026-07-11 da
/// <c>RestoreLostThresholdsAndCategorySeeds</c>, rieliminate da #2880, ripristinate di nuovo da
/// #3648. Nessuno se ne è accorto per mesi perché staging e produzione hanno database preesistenti
/// agli squash: a nascere vuoto è solo un database creato da zero.
/// </para>
/// <para>
/// <b>La regola che spiega quali seed si perdono.</b> Un seed dichiarato con <c>HasData</c> nel
/// modello EF vive nello snapshot, quindi ogni <c>InitialCreate</c> rigenerato lo riemette da solo:
/// è il caso di <c>SystemConfiguration.IncidentBannerState</c>, l'unico sopravvissuto a tutti e tre
/// gli squash. Un seed scritto come <c>migrationBuilder.Sql(...)</c> vive invece solo nel corpo
/// della migration: lo squash lo riscrive a mano e può ometterlo senza che nulla protesti.
/// </para>
/// <para>
/// Questa classe è il posto in cui l'omissione protesta. Asserisce sul <b>database</b> via SQL
/// grezzo, non sulle entità EF, perché ciò che va protetto è l'esito delle migration — non il
/// mapping del modello, che un refactor può cambiare legittimamente.
/// </para>
/// <para>
/// <b>Se aggiungi un seed</b>, aggiungi qui la riga corrispondente. <b>Se un test qui fallisce dopo
/// uno squash</b>, il seed non è stato riportato: recuperalo dalla history
/// (<c>git show &lt;commit&gt;^:&lt;path-migration&gt;</c>) e riprendi i valori alla lettera, non dedurli.
/// </para>
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class MigrationSeedInventoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private string _databaseName = null!;

    public MigrationSeedInventoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_seed_inventory_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(connectionString);
        await _dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<int> CountAsync(string sql)
    {
        var rows = await _dbContext.Database
            .SqlQueryRaw<int>(sql)
            .ToListAsync(TestContext.Current.CancellationToken);

        return rows.Single();
    }

    // ============================================================
    // Seed raw-SQL — quelli che gli squash hanno già perso due volte
    // ============================================================

    [Fact]
    public async Task FreshDatabase_HasCertificationThresholdsSingleton()
    {
        // La tabella dichiara `ck_certification_thresholds_config_singleton` (id = 1): ammette
        // esattamente una riga. Senza il seed è in uno stato che il DB considera valido e il
        // dominio no — ogni lettura delle soglie fallisce.
        var seeded = await CountAsync(
            """
            SELECT COUNT(*)::int AS "Value"
            FROM certification_thresholds_config
            WHERE id = 1
              AND min_coverage_pct = 70
              AND max_page_tolerance = 10
              AND min_bgg_match_pct = 80
              AND min_overall_score = 60
            """);

        seeded.Should().Be(
            1,
            "un database creato da zero deve contenere il singleton delle soglie con i valori di "
                + "CertificationThresholds.Default() — se manca, uno squash ha perso il seed raw-SQL "
                + "della migration (storia in #3646 / #3648 / #3656)");
    }

    [Fact]
    public async Task FreshDatabase_HasEightDefaultGameCategories()
    {
        var seeded = await CountAsync(
            """
            SELECT COUNT(*)::int AS "Value"
            FROM game_categories
            WHERE slug IN (
                'strategy', 'party', 'cooperative', 'deck-building',
                'family', 'abstract', 'thematic', 'euro'
            )
            """);

        seeded.Should().Be(
            8,
            "un database creato da zero deve contenere le 8 categorie di default — se ne mancano, "
                + "uno squash ha perso il seed raw-SQL della migration (storia in #3646 / #3648 / #3656)");
    }

    // ============================================================
    // Seed dichiarativo — non può regredire con uno squash, ma
    // l'inventario è utile solo se è completo
    // ============================================================

    [Fact]
    public async Task FreshDatabase_HasIncidentBannerStateSingleton()
    {
        var seeded = await CountAsync(
            """
            SELECT COUNT(*)::int AS "Value"
            FROM "SystemConfiguration"."IncidentBannerState"
            WHERE "Id" = '00000000-0000-0000-0000-000000000001'
            """);

        seeded.Should().Be(
            1,
            "il singleton del banner incidenti è dichiarato con HasData nel modello EF, quindi ogni "
                + "InitialCreate rigenerato lo riemette: se manca, il problema non è uno squash ma la "
                + "rimozione dell'HasData in IncidentBannerStateEntityConfiguration");
    }
}
