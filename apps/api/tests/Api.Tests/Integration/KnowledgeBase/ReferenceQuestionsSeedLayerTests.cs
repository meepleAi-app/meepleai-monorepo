using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.ReferenceQuestions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.Infrastructure.Seeders.ReferenceQuestions;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>La fixture di <see cref="ReferenceQuestionsSeedLayerTests"/>: l'host vero, un database per test.</summary>
public sealed class ReferenceQuestionsSeedHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "ref_q_seed");

/// <summary>
/// #4470 (ADR-095 fetta 7b): il seeder delle domande di riferimento su Postgres vero. È idempotente per
/// codice, risolve ogni gioco per titolo esatto, e un gioco che manca non ferma gli altri.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4470")]
public sealed class ReferenceQuestionsSeedLayerTests : IClassFixture<ReferenceQuestionsSeedHostFixture>, IAsyncLifetime
{
    private readonly ReferenceQuestionsSeedHostFixture _hostFixture;

    public ReferenceQuestionsSeedLayerTests(ReferenceQuestionsSeedHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _hostFixture.BeginTestAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TheRealSet_RunTwice_GivesTheSameRows_AndBindsCatanToCatan()
    {
        var catan = await SeedGameAsync("Catan");
        var settlers = await SeedGameAsync("The Settlers of Catan");
        var europe = await SeedGameAsync("Ticket to Ride: Europe");
        foreach (var title in new[] { "Carcassonne", "Ticket to Ride", "Gloomhaven" })
            await SeedGameAsync(title);
        var logger = new CapturingLogger();

        await RunAsync(new ReferenceQuestionsSeedLayer(), logger);
        var first = await RowsAsync();
        await RunAsync(new ReferenceQuestionsSeedLayer(), logger);
        var second = await RowsAsync();

        logger.At(LogLevel.Error).Should().BeEmpty();
        var set = ReferenceQuestionSet.LoadEmbedded();
        first.Select(r => r.Code).Should().BeEquivalentTo(set.Select(q => q.Code));
        // ComparingByMembers: Entity<TId> ridefinisce Equals sull'id, e senza questo FluentAssertions
        // confronterebbe solo gli id (una perturbazione che tocca ogni riga passava).
        second.Should().BeEquivalentTo(first, o => o.ComparingByMembers<ReferenceQuestion>().WithStrictOrdering(),
            "la seconda esecuzione non deve inserire, cambiare né toccare nulla");

        first.Where(r => r.Code.StartsWith("CAT-", StringComparison.Ordinal))
            .Should().OnlyContain(r => r.SharedGameId == catan);
        first.Should().NotContain(r => r.SharedGameId == settlers);
        first.Single(r => r.Code == "TTR-18").SharedGameId.Should().Be(europe);
        first.Where(r => r.OutOfCorpus).Select(r => r.Code).Should().BeEquivalentTo(
            set.Where(q => q.OutOfCorpus).Select(q => q.Code));
    }

    [Fact]
    public async Task AChangedText_IsUpdated_AnUnchangedOneIsNotTouched_AndARemovedOneIsKeptWithAWarning()
    {
        var catan = await SeedGameAsync("Catan");
        var v1 = new[] { Definition("CAT-01"), Definition("CAT-02"), Definition("CAT-04") };
        var v2 = new[]
        {
            Definition("CAT-01") with { ExpectedAnswer = "Risposta corretta con l'utente.", InGate = true },
            Definition("CAT-02"),
            Definition("CAT-03"),
        };
        var logger = new CapturingLogger();

        await RunAsync(new ReferenceQuestionsSeedLayer(() => v1), logger);
        var before = (await RowsAsync()).ToDictionary(r => r.Code);
        await RunAsync(new ReferenceQuestionsSeedLayer(() => v2), logger);
        var after = (await RowsAsync()).ToDictionary(r => r.Code);

        after.Keys.Should().BeEquivalentTo(["CAT-01", "CAT-02", "CAT-03", "CAT-04"],
            "CAT-04 non è più nel set ma resta: una valutazione passata può riferirsi a quel codice");
        after["CAT-01"].ExpectedAnswer.Should().Be("Risposta corretta con l'utente.");
        after["CAT-01"].InGate.Should().BeTrue();
        after["CAT-01"].Id.Should().Be(before["CAT-01"].Id, "si aggiorna la riga, non se ne crea un'altra");
        after["CAT-01"].UpdatedAt.Should().BeAfter(before["CAT-01"].UpdatedAt);
        after["CAT-02"].Should().BeEquivalentTo(before["CAT-02"], o => o.ComparingByMembers<ReferenceQuestion>(),
            "una riga uguale al set non si tocca");
        after["CAT-03"].SharedGameId.Should().Be(catan);
        logger.At(LogLevel.Warning).Should().ContainSingle().Which.Should().Contain("CAT-04");
        logger.At(LogLevel.Error).Should().BeEmpty();
    }

    [Fact]
    public async Task AMissingOrAmbiguousGame_IsAnErrorThatNamesIt_AndTheOtherGamesAreLoaded()
    {
        var catan = await SeedGameAsync("Catan");
        var carcassonne = await SeedGameAsync("Carcassonne");
        var twin = await SeedGameAsync("Carcassonne");
        var deleted = await SeedGameAsync("Gloomhaven");
        await SoftDeleteAsync(deleted);
        var logger = new CapturingLogger();

        var seed = () => RunAsync(
            new ReferenceQuestionsSeedLayer(() =>
            [
                Definition("CAT-01"),
                Definition("CAR-01", "Carcassonne"),
                Definition("GLO-01", "Gloomhaven"),
                Definition("GLO-02", "Gloomhaven"),
            ]),
            logger);

        await seed.Should().NotThrowAsync("un'eccezione fermerebbe le layer dopo questa");
        (await RowsAsync()).Should().ContainSingle().Which.Should().Match<ReferenceQuestion>(
            r => r.Code == "CAT-01" && r.SharedGameId == catan);

        var errors = logger.At(LogLevel.Error).ToList();
        errors.Should().HaveCount(2);
        errors.Should().ContainSingle(e => e.Contains("'Gloomhaven'"))
            .Which.Should().Contain("no catalog game").And.Contain("GLO-01").And.Contain("GLO-02",
                "un gioco cancellato in modo logico non conta: per il seeder non c'è");
        errors.Should().ContainSingle(e => e.Contains("'Carcassonne'"))
            .Which.Should().Contain(carcassonne.ToString()).And.Contain(twin.ToString()).And.Contain("CAR-01");
    }

    [Fact]
    public void TheLayer_IsRegistered_AndRunsAfterTheCatalogInStagingAndDev_NotInProd()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var layers = scope.ServiceProvider.GetServices<ISeedLayer>().ToList();

        layers.OfType<ReferenceQuestionsSeedLayer>().Should().ContainSingle();
        foreach (var profile in new[] { SeedProfile.Staging, SeedProfile.Dev })
        {
            var ordered = SeedOrchestrator.FilterLayers(layers, profile).ToList();
            var catalog = ordered.FindIndex(l => l is CatalogSeedLayer);
            var questions = ordered.FindIndex(l => l is ReferenceQuestionsSeedLayer);

            catalog.Should().BeGreaterThanOrEqualTo(0);
            questions.Should().BeGreaterThan(catalog, $"nel profilo {profile} i giochi li crea il catalogo, prima");
        }

        SeedOrchestrator.FilterLayers(layers, SeedProfile.Prod).OfType<ReferenceQuestionsSeedLayer>()
            .Should().BeEmpty("in produzione il catalogo non si semina");
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static ReferenceQuestionDefinition Definition(string code, string game = "Catan") => new(
        Code: code,
        Game: game,
        Question: $"Domanda {code}?",
        ExpectedAnswer: $"Risposta {code}.",
        Category: "Commercio",
        Difficulty: "Medium",
        QuestionType: "Procedurale",
        SourceReference: "Manuale - Commercio",
        OutOfCorpus: false,
        InGate: false);

    private async Task RunAsync(ReferenceQuestionsSeedLayer layer, CapturingLogger logger)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var context = new SeedContext(SeedProfile.Staging, db, scope.ServiceProvider, logger, Guid.Empty);
        await layer.SeedAsync(context, Ct);
    }

    private async Task<List<ReferenceQuestion>> RowsAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ReferenceQuestions.AsNoTracking().OrderBy(q => q.Code).ToListAsync(Ct);
    }

    private async Task<Guid> SeedGameAsync(string title)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.SeedSharedGameAsync(db, title);
    }

    private async Task SoftDeleteAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.SharedGames.IgnoreQueryFilters()
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsDeleted, true), Ct);
    }
}
