using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Concorrenza ottimistica su <c>game_session_states</c> (#3651, lotto 10).
///
/// <para>
/// Lo stato di una partita in corso è il dato più conteso che esista in questo dominio: ogni mossa
/// lo riscrive, e più client possono inviarne una insieme. Senza token attivo l'ultima scrittura
/// vince e una mossa sparisce dal tavolo senza che nulla lo segnali.
/// </para>
/// <para>
/// Il commento sull'entità dichiarava «Optimistic locking via PostgreSQL xmin (EF Core Timestamp)»
/// — la <b>quarta</b> affermazione di questo tipo incontrata da #3651, e falsa come le altre tre:
/// <c>[Timestamp]</c> su un <c>byte[]</c> produce una colonna <c>bytea</c>, che Postgres non
/// popola.
/// </para>
/// <para>
/// Write-path: <c>GameSessionStateRepository.UpdateAsync</c> stacca l'entità tracciata e riattacca
/// un grafo <b>detached</b> (<c>:60-69</c>), quindi la conversione richiedeva anche il round-trip
/// del token nel mapper — altrimenti il difetto si sarebbe spostato da «non protegge nulla» a
/// «rifiuta ogni scrittura» (#3688). È ciò che verifica il secondo test.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "3651")]
public sealed class GameSessionStateXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameSessionStateXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamestate_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>
    /// `GameSessionId` ha una FK verso <c>game_sessions</c>, che a sua volta referenzia
    /// <c>shared_games</c>: entrambe vanno soddisfatte prima di arrivare al token (pitfall #2620).
    /// </summary>
    private async Task<Guid> SeedStateAsync()
    {
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Gioco in corso",
        });

        var gameSessionId = Guid.NewGuid();
        _dbContext.GameSessions.Add(new GameSessionEntity
        {
            Id = gameSessionId,
            GameId = gameId,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            PlayersJson = "[]",
        });

        var id = Guid.NewGuid();
        _dbContext.GameSessionStates.Add(new GameSessionStateEntity
        {
            Id = id,
            GameSessionId = gameSessionId,
            TemplateId = Guid.NewGuid(),
            CurrentStateJson = """{"turn":1}""",
            Version = 1,
            LastUpdatedAt = DateTime.UtcNow,
            LastUpdatedBy = "seed",
        });

        await _dbContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedStateAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        // Issue #3866: `.AsTracking()` is REQUIRED here. The DbContext default is NoTracking
        // (PERF-06), so a plain read hands back a DETACHED entity: the mutations below would reach
        // no change tracker, SaveChangesAsync would write nothing, and the concurrency token this
        // test exists to exercise would never even be compared. This is the documented opt-out for
        // a fixture whose subject IS a tracked read-modify-write.
        var seenByFirst = await firstContext.GameSessionStates.AsTracking().FirstAsync(s => s.Id == id);
        var seenBySecond = await secondContext.GameSessionStates.AsTracking().FirstAsync(s => s.Id == id);

        seenByFirst.CurrentStateJson = """{"turn":2}""";
        seenByFirst.Version = 2;
        await firstContext.SaveChangesAsync();

        // Il secondo client invia la sua mossa partendo dallo stato del turno 1: senza conflitto
        // rilevato, sovrascrive il turno 2 e la mossa dell'altro giocatore sparisce.
        seenBySecond.CurrentStateJson = """{"turn":2,"alt":true}""";
        seenBySecond.Version = 2;

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due mosse concorrenti sulla stessa partita devono escludersi: senza conflitto "
            + "rilevato l'ultima vince e una mossa sparisce dal tavolo (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedStateAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var state = await context.GameSessionStates.AsTracking().FirstAsync(s => s.Id == id); // #3866
        state.CurrentStateJson = """{"turn":3}""";

        var write = async () => await context.SaveChangesAsync();

        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare: se anche questa fallisse, il token sarebbe "
            + "acceso ma il write-path non lo preserverebbe (#3688)");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.GameSessionStates.FirstAsync(s => s.Id == id);
        JsonDocument.Parse(persisted.CurrentStateJson).RootElement.GetProperty("turn").GetInt32()
            .Should().Be(3);
    }
}
