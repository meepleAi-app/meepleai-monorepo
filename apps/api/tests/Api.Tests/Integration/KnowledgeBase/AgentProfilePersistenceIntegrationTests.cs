using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Persistenza di <see cref="AgentProfile"/> su Postgres vero (ADR-095, #4167): il seed della
/// versione 1, una pubblicazione che attraversa la unit of work, la concorrenza ottimistica sulla
/// radice (ADR-060) e il vincolo «al più una bozza» nello schema. Dal #4179 un database nuovo
/// porta anche la versione 2, ripubblicata dalla migration con i modelli correnti, e dal #4238 (A2) la
/// versione 3, con l'assistente in partita abilitato.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4167")]
[Trait("Issue", "4179")]
[Trait("Issue", "4238")]
public sealed class AgentProfilePersistenceIntegrationTests : IAsyncLifetime
{
    private const string RepublishMigration = "20261011061616_RepublishAgentProfileWithCurrentModels";
    private const string AssistantMigration = "20261015073526_PublishAgentProfileWithSessionAssistant";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public AgentProfilePersistenceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"agent_profile_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>
    /// I valori che erano fissi nel codice al seed della versione 1, scritti qui a mano e non letti
    /// dal codice: è la migration a doverli riprodurre. Fonti: <c>HybridLlmService</c> (temperatura,
    /// token), <c>DefaultStrategyModelMappings</c> (Balanced → modelli), il prompt di
    /// <c>AskQuestionQueryHandler</c>, i parametri di recupero e fusione, nessun filtro per categoria.
    /// </summary>
    private static AgentProfileContent SeedValues() =>
        HardcodedValues(primaryModelId: "deepseek-chat", fallbackModelId: "meta-llama/llama-3.3-70b-instruct:free");

    /// <summary>
    /// #4179: gli stessi valori con i due modelli correnti, quelli della versione 2.
    /// </summary>
    private static AgentProfileContent CurrentModelValues() =>
        HardcodedValues(primaryModelId: "deepseek-flash", fallbackModelId: "meta-llama/llama-3.3-70b-instruct");

    /// <summary>
    /// #4238: la sezione «Assistente in partita» che la migration pubblica. I numeri sono scritti a mano,
    /// i valori raccomandati in spec §7.1; i prompt vengono dal file misurato in #4270, non dal codice.
    /// </summary>
    private static SessionAssistantSettings AssistantSection(string promptFile = "v3.json")
    {
        using var measured = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepoRoot(), "tests", "llm-eval", "session-assistant", "prompts", promptFile)));
        return SessionAssistantSettings.Create(
            enabled: true,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["it"] = measured.RootElement.GetProperty("italian").GetString()!,
                ["en"] = measured.RootElement.GetProperty("english").GetString()!,
            },
            temperature: 0.2m,
            maxResponseTokens: 600,
            maxToolIterations: 3,
            turnTimeoutSeconds: 15,
            useReserveModel: false);
    }

    /// <summary>#4238: i valori della versione 2 con l'assistente abilitato, quelli della versione 3.</summary>
    private static AgentProfileContent AssistantValues() =>
        HardcodedValues("deepseek-flash", "meta-llama/llama-3.3-70b-instruct", AssistantSection());

    /// <summary>#4411: la versione 3 con il prompt v4 dell'assistente, misurato con A5: quella della versione 4.</summary>
    private static AgentProfileContent AssistantV4Values() =>
        HardcodedValues("deepseek-flash", "meta-llama/llama-3.3-70b-instruct", AssistantSection("v4.json"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "llm-eval")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Radice del repository non trovata.");
    }

    private static AgentProfileContent HardcodedValues(
        string primaryModelId, string fallbackModelId, SessionAssistantSettings? sessionAssistant = null) =>
        AgentProfileContent.Create(
            name: "MeepleAI",
            description: "Specialized agent for board game rules interpretation and clarification.",
            persona: "a precise board game rules assistant",
            primaryModelId: primaryModelId,
            fallbackModelId: fallbackModelId,
            temperature: 0.3m,
            maxResponseTokens: 1500,
            citationStyle: CitationStyle.PageReferencesInText,
            systemPrompts: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["en"] = "You are MeepleAI, a precise board game rules assistant. "
                    + "Answer ONLY using the provided rulebook context. "
                    + "For each claim you make, it MUST be directly supported by the context provided. "
                    + "If the context does not contain the answer, respond EXACTLY with: "
                    + "'This information is not available in the provided rulebook.' "
                    + "Never invent rules, game mechanics, or examples not present in the context. "
                    + "Always cite the page number in brackets, e.g. [Page 3].",
            },
            fallbackLanguage: "en",
            candidatePoolSize: 20,
            finalTopK: 5,
            minScore: 0.55m,
            vectorWeight: 0.7m,
            keywordWeight: 0.3m,
            rerankerEnabled: true,
            allowedCategories: Enum.GetValues<DocumentCategory>(),
            sessionAssistant: sessionAssistant ?? SessionAssistantSettings.Disabled);

    [Fact]
    public async Task FreshDatabase_PublishesVersionFour_WithThePromptV4_AndArchivesTheEarlierOnes()
    {
        var profile = await new AgentProfileRepository(_dbContext).GetAsync(TestContext.Current.CancellationToken);

        profile.Should().NotBeNull("la tabella ammette una sola riga e il seed deve crearla");
        profile!.Versions.Should().HaveCount(4);
        profile.Draft.Should().BeNull();
        profile.Published.VersionNumber.Should().Be(4);
        profile.Published.Content.Should().Be(AssistantV4Values(),
            "#4411: la migration pubblica la versione 3 con il prompt v4 dell'assistente, misurato con A5");
        var withV3 = profile.Versions.Single(v => v.VersionNumber == 3);
        withV3.Status.Should().Be(AgentProfileVersionStatus.Archived);
        withV3.Content.Should().Be(AssistantValues(),
            "#4238: la migration pubblica la versione 2 con l'assistente in partita abilitato");
        var republished = profile.Versions.Single(v => v.VersionNumber == 2);
        republished.Status.Should().Be(AgentProfileVersionStatus.Archived);
        republished.Content.Should().Be(CurrentModelValues(),
            "#4179: la versione 1 nomina due modelli dismessi, e la migration ne pubblica una con i correnti");
        var seed = profile.Versions.Single(v => v.VersionNumber == 1);
        seed.Status.Should().Be(AgentProfileVersionStatus.Archived);
        seed.Content.Should().Be(SeedValues(), "una versione pubblicata non si modifica: si archivia");
    }

    /// <summary>
    /// #4179: la ripubblicazione scatta solo se la versione pubblicata nomina ancora un modello
    /// dismesso. Un profilo già portato su modelli correnti non riceve una versione in più.
    /// </summary>
    [Fact]
    public async Task RepublishMigration_LeavesAProfileWithCurrentModelsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"agent_profile_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = _fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261010220145_AddLlmCostLogPricingStatus", ct);
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE knowledge_base.agent_profile_versions
                SET content = content || jsonb_build_object(
                    'primaryModelId', 'deepseek-flash',
                    'fallbackModelId', 'meta-llama/llama-3.3-70b-instruct')
                WHERE status = 1
                """,
                ct);

            await migrator.MigrateAsync(RepublishMigration, ct);

            var profile = await new AgentProfileRepository(context).GetAsync(ct);
            profile!.Versions.Should().ContainSingle("la versione pubblicata nomina già i modelli correnti");
            profile.Published.Content.Should().Be(CurrentModelValues());
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// #4179: la migration sostituisce solo la chiave che nomina ancora un modello dismesso. Un
    /// modello principale scelto dall'admin resta, anche se la riserva va sostituita.
    /// </summary>
    [Fact]
    public async Task RepublishMigration_ReplacesOnlyTheRetiredModel_AndKeepsTheOtherChoice()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"agent_profile_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = _fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261010220145_AddLlmCostLogPricingStatus", ct);
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE knowledge_base.agent_profile_versions
                SET content = content || jsonb_build_object('primaryModelId', 'anthropic/claude-sonnet-4.5')
                WHERE status = 1
                """,
                ct);

            await migrator.MigrateAsync(RepublishMigration, ct);

            var profile = await new AgentProfileRepository(context).GetAsync(ct);
            profile!.Published.VersionNumber.Should().Be(2);
            profile.Published.Content.PrimaryModelId.Should().Be("anthropic/claude-sonnet-4.5",
                "il principale non è un modello dismesso: è una scelta da non toccare");
            profile.Published.Content.FallbackModelId.Should().Be("meta-llama/llama-3.3-70b-instruct");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// #4238: la migration scrive ogni campo della sezione. Un campo assente varrebbe il default del codice,
    /// che può cambiare dopo la misura: si guarda il jsonb, non il value object che i default li riempie.
    /// </summary>
    [Fact]
    public async Task AssistantMigration_WritesEveryFieldOfTheSection_InTheJsonb()
    {
        var keys = await _dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT jsonb_object_keys(content->'sessionAssistant') AS "Value"
                FROM knowledge_base.agent_profile_versions
                WHERE status = 1
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        keys.Should().BeEquivalentTo(
            "enabled", "systemPrompts", "temperature", "maxResponseTokens", "maxToolIterations", "turnTimeoutSeconds",
            "useReserveModel");
    }

    /// <summary>#4238: un profilo che ha già l'assistente abilitato non riceve una versione in più.</summary>
    [Fact]
    public async Task AssistantMigration_LeavesAProfileWithTheAssistantOnUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"agent_profile_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = _fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(RepublishMigration, ct);
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE knowledge_base.agent_profile_versions
                SET content = content || jsonb_build_object('sessionAssistant', jsonb_build_object(
                    'enabled', true, 'systemPrompts', jsonb_build_object('it', 'Un prompt scelto altrove.')))
                WHERE status = 1
                """,
                ct);

            await migrator.MigrateAsync(AssistantMigration, ct);

            var profile = await new AgentProfileRepository(context).GetAsync(ct);
            profile!.Published.VersionNumber.Should().Be(2, "l'assistente era già abilitato");
            profile.Published.Content.SessionAssistant.PromptFor("it").Should().Be("Un prompt scelto altrove.");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// #4238: una bozza scritta prima della migration non ha la sezione. La pagina admin la conserva dalla
    /// bozza, quindi pubblicarla spegnerebbe l'assistente in silenzio: la migration la aggiunge anche lì.
    /// </summary>
    [Fact]
    public async Task AssistantMigration_GivesTheSectionToADraftThatLacksIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"agent_profile_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = _fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(RepublishMigration, ct);
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO knowledge_base.agent_profile_versions
                    (id, agent_profile_id, version_number, status, content, created_at)
                SELECT gen_random_uuid(), agent_profile_id, 3, 0, content || jsonb_build_object('temperature', 0.5), NOW()
                FROM knowledge_base.agent_profile_versions
                WHERE status = 1
                """,
                ct);

            await migrator.MigrateAsync(AssistantMigration, ct);

            var profile = await new AgentProfileRepository(context).GetAsync(ct);
            profile!.Draft!.VersionNumber.Should().Be(3);
            profile.Draft.Content.Temperature.Should().Be(0.5m, "il resto della bozza non si tocca");
            profile.Draft.Content.SessionAssistant.Should().Be(AssistantSection());
            profile.Published.VersionNumber.Should().Be(4);
            profile.Published.Content.Should().Be(AssistantValues());
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    /// <summary>#4238: il Down ripristina la versione 2 come pubblicata, e toglie la versione 3.</summary>
    [Fact]
    public async Task AssistantMigration_Down_RestoresVersionTwo()
    {
        var ct = TestContext.Current.CancellationToken;

        await _dbContext.GetService<IMigrator>().MigrateAsync(RepublishMigration, ct);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var profile = await new AgentProfileRepository(verification).GetAsync(ct);
        profile!.Versions.Should().HaveCount(2);
        profile.Published.VersionNumber.Should().Be(2);
        profile.Published.Content.Should().Be(CurrentModelValues());
    }

    /// <summary>#4411: la migrazione del prompt v4 cambia solo il prompt di un profilo che ha ancora la v3.</summary>
    [Fact]
    public async Task PromptV4Migration_LeavesAPromptChosenElsewhereUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"agent_profile_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = _fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(AssistantMigration, ct);
            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE knowledge_base.agent_profile_versions
                SET content = jsonb_set(content, ARRAY['sessionAssistant','systemPrompts','it'], to_jsonb('Un prompt scelto altrove.'::text))
                WHERE status = 1
                """,
                ct);

            await migrator.MigrateAsync(cancellationToken: ct);

            var profile = await new AgentProfileRepository(context).GetAsync(ct);
            profile!.Published.VersionNumber.Should().Be(3, "il prompt non era più la v3");
            profile.Published.Content.SessionAssistant.PromptFor("it").Should().Be("Un prompt scelto altrove.");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    /// <summary>#4411: una bozza con il prompt v3 riceve la v4; il resto della bozza non cambia.</summary>
    [Fact]
    public async Task PromptV4Migration_MovesADraftWithTheV3PromptToV4()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"agent_profile_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);
        try
        {
            await using var context = _fixture.CreateDbContext(connectionString);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(AssistantMigration, ct);
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO knowledge_base.agent_profile_versions
                    (id, agent_profile_id, version_number, status, content, created_at)
                SELECT gen_random_uuid(), agent_profile_id, 4, 0, content || jsonb_build_object('temperature', 0.5), NOW()
                FROM knowledge_base.agent_profile_versions
                WHERE status = 1
                """,
                ct);

            await migrator.MigrateAsync(cancellationToken: ct);

            var profile = await new AgentProfileRepository(context).GetAsync(ct);
            profile!.Draft!.VersionNumber.Should().Be(4);
            profile.Draft.Content.Temperature.Should().Be(0.5m, "il resto della bozza non si tocca");
            profile.Draft.Content.SessionAssistant.Should().Be(AssistantSection("v4.json"));
            profile.Published.VersionNumber.Should().Be(5);
            profile.Published.Content.Should().Be(AssistantV4Values());
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    /// <summary>#4411: il Down ripristina la versione 3, con il prompt v3, come pubblicata.</summary>
    [Fact]
    public async Task PromptV4Migration_Down_RestoresVersionThree()
    {
        var ct = TestContext.Current.CancellationToken;

        await _dbContext.GetService<IMigrator>().MigrateAsync(AssistantMigration, ct);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var profile = await new AgentProfileRepository(verification).GetAsync(ct);
        profile!.Versions.Should().HaveCount(3);
        profile.Published.VersionNumber.Should().Be(3);
        profile.Published.Content.Should().Be(AssistantValues());
    }

    [Fact]
    public async Task PublishingADraft_IsPersisted_AndArchivesThePublishedVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new AgentProfileRepository(_dbContext);
        var profile = await repository.GetAsync(ct);

        profile!.CreateDraft(AgentProfileTests.Content(temperature: 0.5m));
        await _dbContext.SaveChangesAsync(ct);
        profile.PublishDraft(AgentProfileTests.PricingTheModelsOf(profile.Draft!.Content));
        await _dbContext.SaveChangesAsync(ct);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await new AgentProfileRepository(verification).GetAsync(ct);
        persisted!.Published.VersionNumber.Should().Be(5);
        persisted.Published.Content.Temperature.Should().Be(0.5m);
        persisted.Versions.Single(v => v.VersionNumber == 4).Status.Should().Be(AgentProfileVersionStatus.Archived);
        persisted.Versions.Single(v => v.VersionNumber == 4).Content.Should().Be(AssistantV4Values());
    }

    /// <summary>
    /// Il caso che solo xmin può fermare: nessun indice protegge il contenuto di una versione. Il
    /// primo amministratore pubblica la bozza; il secondo, con una copia letta prima, la modifica
    /// ancora come bozza. Senza il controllo sulla radice riscriverebbe una versione già pubblicata.
    /// </summary>
    [Fact]
    public async Task EditingADraftPublishedMeanwhile_IsRejected_AndThePublishedContentIsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = await new AgentProfileRepository(_dbContext).GetAsync(ct);
        profile!.CreateDraft(AgentProfileTests.Content(temperature: 0.5m));
        await _dbContext.SaveChangesAsync(ct);

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);
        var seenByFirst = await new AgentProfileRepository(firstContext).GetAsync(ct);
        var seenBySecond = await new AgentProfileRepository(secondContext).GetAsync(ct);

        seenByFirst!.PublishDraft(AgentProfileTests.PricingTheModelsOf(seenByFirst.Draft!.Content));
        await firstContext.SaveChangesAsync(ct);

        seenBySecond!.UpdateDraft(AgentProfileTests.Content(temperature: 0.9m));
        var secondWrite = async () => await secondContext.SaveChangesAsync(ct);

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "la radice cambia a ogni comando, quindi il suo xmin respinge la scrittura basata su "
            + "una copia in cui la versione 5 era ancora una bozza");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await new AgentProfileRepository(verification).GetAsync(ct);
        persisted!.Published.VersionNumber.Should().Be(5);
        persisted.Published.Content.Temperature.Should().Be(0.5m);
    }

    /// <summary>
    /// Due bozze concorrenti le ferma lo schema prima di xmin: EF esegue l'INSERT della versione
    /// prima dell'UPDATE della radice, e l'INSERT urta l'indice della bozza. Misurato, non dedotto:
    /// per questo l'asserzione nomina il vincolo invece di aspettarsi un conflitto di concorrenza.
    /// </summary>
    [Fact]
    public async Task ConcurrentDrafts_TheSecondIsRejectedByTheDraftIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);
        var seenByFirst = await new AgentProfileRepository(firstContext).GetAsync(ct);
        var seenBySecond = await new AgentProfileRepository(secondContext).GetAsync(ct);

        seenByFirst!.CreateDraft(AgentProfileTests.Content(temperature: 0.5m));
        await firstContext.SaveChangesAsync(ct);

        // La copia del secondo amministratore non vede la bozza del primo: in memoria la crea.
        seenBySecond!.CreateDraft(AgentProfileTests.Content(temperature: 0.9m));
        var secondWrite = async () => await secondContext.SaveChangesAsync(ct);

        (await secondWrite.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("ux_agent_profile_versions_one_draft");
    }

    /// <summary>
    /// Il riscontro nello schema di «al più una bozza». Esiste perché la prima stesura della
    /// configurazione lo perdeva in silenzio: due HasIndex senza nome sulla stessa colonna sono per
    /// EF un indice solo, e il secondo filtro sostituiva il primo.
    /// </summary>
    [Fact]
    public async Task Schema_RejectsASecondDraft_EvenBypassingTheAggregate()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = await new AgentProfileRepository(_dbContext).GetAsync(ct);
        profile!.CreateDraft(AgentProfileTests.Content(temperature: 0.5m));
        await _dbContext.SaveChangesAsync(ct);

        var insertSecondDraft = async () => await _dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO knowledge_base.agent_profile_versions
                (id, agent_profile_id, version_number, status, content, created_at)
            SELECT gen_random_uuid(), agent_profile_id, 99, status, content, created_at
            FROM knowledge_base.agent_profile_versions
            WHERE status = 0
            """,
            ct);

        (await insertSecondDraft.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ux_agent_profile_versions_one_draft");
    }

    /// <summary>
    /// «Esattamente una pubblicata» è un vincolo differito, controllato al commit: un indice unico
    /// faceva fallire a caso le pubblicazioni legittime (vedi il test di pubblicazione, che con
    /// l'indice falliva due run su tre). Differito non vuol dire assente: una seconda pubblicata
    /// che sopravvive fino al commit viene respinta.
    /// </summary>
    [Fact]
    public async Task Schema_RejectsASecondPublishedVersion_AtCommit()
    {
        var ct = TestContext.Current.CancellationToken;

        var insertSecondPublished = async () => await _dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO knowledge_base.agent_profile_versions
                (id, agent_profile_id, version_number, status, content, created_at)
            SELECT gen_random_uuid(), agent_profile_id, 99, status, content, created_at
            FROM knowledge_base.agent_profile_versions
            WHERE status = 1
            """,
            ct);

        (await insertSecondPublished.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("ex_agent_profile_versions_one_published");
    }
}
