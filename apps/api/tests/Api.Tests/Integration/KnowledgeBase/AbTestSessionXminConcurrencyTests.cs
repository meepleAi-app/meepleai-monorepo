using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Concorrenza ottimistica su <c>knowledge_base.ab_test_sessions</c> (#3651, lotto 5).
///
/// <para>
/// La race è nel dominio: una sessione A/B è un confronto cieco che un valutatore avvia e un
/// altro può avviare nello stesso momento. Senza token attivo entrambe le transizioni
/// <c>Draft → InProgress</c> riescono, e la seconda sovrascrive lo stato della prima senza che
/// nulla lo segnali.
/// </para>
/// <para>
/// <b>Perché prima della conversione.</b> Il token era <c>byte[] RowVersion</c> su colonna
/// <c>bytea</c>: Postgres non la popola, EF confronta <c>NULL = NULL</c> e nessun conflitto viene
/// rilevato. Il test deve fallire con «no exception was thrown».
/// </para>
/// <para>
/// I test passano dal <see cref="AbTestSessionRepository"/> e non dal DbContext, di proposito:
/// quel repository legge <c>AsNoTracking()</c> (<c>:26</c>) e scrive con <c>Update()</c> su un
/// grafo <b>detached</b> (<c>:87</c>). È la combinazione che in #3688 rompe ogni scrittura quando
/// il token non attraversa il write-path — qui l'entità di dominio è essa stessa l'entità EF e
/// trasporta il token letto, ma è esattamente ciò che il secondo test verifica invece di assumere.
/// </para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3651")]
public sealed class AbTestSessionXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public AbTestSessionXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"abtest_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static AbTestSessionRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Una sessione con due varianti: <c>StartTest()</c> ne richiede almeno due, ed è la
    /// transizione più semplice che il dominio espone per provocare una scrittura.
    /// </summary>
    private async Task<Guid> SeedStartableSessionAsync()
    {
        var session = AbTestSession.Create(Guid.NewGuid(), "Which model explains the Catan setup better?");
        session.AddVariant("A", "openrouter", "model-a");
        session.AddVariant("B", "openrouter", "model-b");

        await CreateRepository(_dbContext).AddAsync(session);
        return session.Id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedStartableSessionAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        var seenByFirst = await CreateRepository(firstContext).GetByIdWithVariantsAsync(id);
        var seenBySecond = await CreateRepository(secondContext).GetByIdWithVariantsAsync(id);

        seenByFirst!.StartTest();
        await CreateRepository(firstContext).UpdateAsync(seenByFirst);

        // Il secondo valutatore ha ancora la sessione in Draft nella sua copia: la transizione
        // riesce in memoria, ed è la scrittura che deve essere respinta.
        seenBySecond!.StartTest();

        var secondWrite = async () => await CreateRepository(secondContext).UpdateAsync(seenBySecond);

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due avvii concorrenti della stessa sessione A/B devono escludersi: senza conflitto "
            + "rilevato la seconda transizione sovrascrive la prima in silenzio (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedStartableSessionAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        var session = await repository.GetByIdWithVariantsAsync(id);
        session!.StartTest();

        var write = async () => await repository.UpdateAsync(session);

        // L'altra direzione, e qui non è ceremonia: il repository legge AsNoTracking e scrive con
        // Update() su detached. Se il token non attraversasse quel percorso, la WHERE conterrebbe
        // xmin = 0 e QUESTA scrittura fallirebbe — il difetto di #3688, opposto a quello di #3651.
        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare anche attraverso Update() su grafo detached");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await CreateRepository(verification).GetByIdAsync(id);
        persisted!.Status.Should().Be(Api.BoundedContexts.KnowledgeBase.Domain.Enums.AbTestStatus.InProgress);
    }
}
