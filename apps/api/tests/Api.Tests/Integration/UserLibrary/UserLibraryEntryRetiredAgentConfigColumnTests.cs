using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Consegna «expand» del ritiro della configurazione agente per gioco (#4138).
///
/// <para>
/// La colonna <c>CustomAgentConfigJson</c> non è più letta né scritta dal codice, ma resta nello
/// schema finché una consegna successiva non la elimina: è ciò che rende sicuro un rollback del
/// codice, che quella colonna la legge ancora.
/// </para>
/// <para>
/// <b>Cosa fissa.</b> <c>UserLibraryRepository.UpdateAsync</c> fa <c>DbSet.Update(entity)</c> su un
/// grafo <b>detached</b>. Misurato: EF Core marca come modificate le proprietà della classe ma
/// <b>non</b> una shadow property di cui non conosce il valore, quindi oggi l'update lascia la
/// colonna com'è. Il test lo fissa perché la regressione sarebbe invisibile: se la colonna tornasse
/// una proprietà della classe, o venisse marcata come modificata, ogni salvataggio la scriverebbe a
/// <c>NULL</c>, il rollback troverebbe le configurazioni perse, e nessun altro test lo vedrebbe,
/// perché nessuno legge più quella colonna. Verificato per perturbazione: forzando
/// <c>IsModified = true</c> sulla shadow property il test fallisce.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4138")]
public sealed class UserLibraryEntryRetiredAgentConfigColumnTests : IAsyncLifetime
{
    private const string StoredConfig =
        """{"LlmModel": "llama-3.3-70b-free", "Temperature": 0.7, "MaxTokens": 4096, "Personality": "Amichevole", "DetailLevel": "Normale", "PersonalNotes": null}""";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public UserLibraryEntryRetiredAgentConfigColumnTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"userlib_agentcol_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static UserLibraryRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Semina una voce con una configurazione salvata, come quelle scritte prima del ritiro.
    /// </summary>
    private async Task<Guid> SeedEntryWithStoredConfigAsync()
    {
        // FK su shared_games e users da soddisfare prima, altrimenti 23503 (pitfall #2620).
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Gioco con configurazione agente",
        });

        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = $"agentcol-{userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });

        var id = Guid.NewGuid();
        var entity = new UserLibraryEntryEntity
        {
            Id = id,
            UserId = userId,
            GameId = gameId,
            Notes = "prima nota",
            IsFavorite = false,
            AddedAt = DateTime.UtcNow,
        };
        _dbContext.UserLibraryEntries.Add(entity);
        _dbContext.Entry(entity)
            .Property(UserLibraryEntryEntity.RetiredCustomAgentConfigJson)
            .CurrentValue = StoredConfig;
        await _dbContext.SaveChangesAsync();
        return id;
    }

    private async Task<string?> ReadStoredConfigAsync(Guid id)
    {
        await using var verification = _fixture.CreateDbContext(_connectionString);
        return await verification.UserLibraryEntries
            .Where(e => e.Id == id)
            .Select(e => EF.Property<string?>(e, UserLibraryEntryEntity.RetiredCustomAgentConfigJson))
            .SingleAsync();
    }

    [Fact]
    public async Task Update_ThroughRepository_LeavesRetiredAgentConfigColumnUntouched()
    {
        var id = await SeedEntryWithStoredConfigAsync();
        (await ReadStoredConfigAsync(id)).Should().NotBeNull("il seed deve aver scritto la colonna");

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        var entry = await repository.GetByIdAsync(id);
        entry.Should().NotBeNull();
        entry!.UpdateNotes(new Api.BoundedContexts.UserLibrary.Domain.ValueObjects.LibraryNotes("nota aggiornata"));
        await repository.UpdateAsync(entry);
        await context.SaveChangesAsync();

        await using var verification = _fixture.CreateDbContext(_connectionString);
        (await verification.UserLibraryEntries.SingleAsync(e => e.Id == id)).Notes
            .Should().Be("nota aggiornata", "l'update deve essere arrivato, altrimenti il test non prova niente");

        (await ReadStoredConfigAsync(id)).Should().NotBeNull(
            "la colonna ritirata resta intatta fino alla consegna che la elimina: un update che la "
            + "riscrive a NULL svuoterebbe in silenzio i dati che un rollback del codice si aspetta");
    }
}
