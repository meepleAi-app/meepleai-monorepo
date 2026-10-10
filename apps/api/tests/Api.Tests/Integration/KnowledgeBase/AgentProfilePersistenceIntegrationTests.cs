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
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Persistenza di <see cref="AgentProfile"/> su Postgres vero (ADR-095, #4167): il seed della
/// versione 1, una pubblicazione che attraversa la unit of work, la concorrenza ottimistica sulla
/// radice (ADR-060) e il vincolo «al più una bozza» nello schema.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4167")]
public sealed class AgentProfilePersistenceIntegrationTests : IAsyncLifetime
{
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
    /// I valori che oggi sono fissi nel codice, scritti qui a mano e non letti dal codice: è la
    /// migration a doverli riprodurre. Fonti: <c>HybridLlmService</c> (temperatura, token),
    /// <c>DefaultStrategyModelMappings</c> (Balanced → modelli), il prompt di
    /// <c>AskQuestionQueryHandler</c>, i parametri di recupero e fusione, nessun filtro per categoria.
    /// </summary>
    private static AgentProfileContent TodaysHardcodedValues() =>
        AgentProfileContent.Create(
            name: "MeepleAI",
            description: "Specialized agent for board game rules interpretation and clarification.",
            persona: "a precise board game rules assistant",
            primaryModelId: "deepseek-chat",
            fallbackModelId: "meta-llama/llama-3.3-70b-instruct:free",
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
            allowedCategories: Enum.GetValues<DocumentCategory>());

    [Fact]
    public async Task FreshDatabase_HasVersionOnePublished_WithTodaysHardcodedValues()
    {
        var profile = await new AgentProfileRepository(_dbContext).GetAsync(TestContext.Current.CancellationToken);

        profile.Should().NotBeNull("la tabella ammette una sola riga e il seed deve crearla");
        profile!.Versions.Should().ContainSingle();
        profile.Draft.Should().BeNull();
        profile.Published.VersionNumber.Should().Be(1);
        profile.Published.Content.Should().Be(TodaysHardcodedValues());
    }

    [Fact]
    public async Task PublishingADraft_IsPersisted_AndArchivesVersionOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new AgentProfileRepository(_dbContext);
        var profile = await repository.GetAsync(ct);

        profile!.CreateDraft(AgentProfileTests.Content(temperature: 0.5m));
        await _dbContext.SaveChangesAsync(ct);
        profile.PublishDraft();
        await _dbContext.SaveChangesAsync(ct);

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await new AgentProfileRepository(verification).GetAsync(ct);
        persisted!.Published.VersionNumber.Should().Be(2);
        persisted.Published.Content.Temperature.Should().Be(0.5m);
        persisted.Versions.Single(v => v.VersionNumber == 1).Status.Should().Be(AgentProfileVersionStatus.Archived);
        persisted.Versions.Single(v => v.VersionNumber == 1).Content.Should().Be(TodaysHardcodedValues());
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

        seenByFirst!.PublishDraft();
        await firstContext.SaveChangesAsync(ct);

        seenBySecond!.UpdateDraft(AgentProfileTests.Content(temperature: 0.9m));
        var secondWrite = async () => await secondContext.SaveChangesAsync(ct);

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "la radice cambia a ogni comando, quindi il suo xmin respinge la scrittura basata su "
            + "una copia in cui la versione 2 era ancora una bozza");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await new AgentProfileRepository(verification).GetAsync(ct);
        persisted!.Published.VersionNumber.Should().Be(2);
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
