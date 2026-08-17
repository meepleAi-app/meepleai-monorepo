using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Concorrenza ottimistica su <c>sessions</c> (#3651, lotto 10).
///
/// <para>
/// È la riga con la superficie più larga del lotto: una sessione di gioco viene toccata da più
/// partecipanti insieme — punteggi, note, stato — ed è esattamente il dominio in cui due schede
/// aperte sono la norma e non l'eccezione. Senza token attivo l'ultima scrittura vince e ciò che
/// si perde è il lavoro di un altro giocatore.
/// </para>
/// <para>
/// Write-path: <c>SessionMapper</c> fa già il round-trip del token — lo riporta in
/// <c>MapToPersistence</c> (<c>:37</c>) e lo ricarica in <c>MapToDomain</c> (<c>:74</c>). È il
/// motivo per cui qui basta cambiare il <b>tipo</b> del token: l'infrastruttura per portarlo fino
/// a <c>Update()</c> esisteva già, mancava solo che il token significasse qualcosa (#3688).
/// </para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "3651")]
public sealed class SessionXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public SessionXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"session_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<Guid> SeedSessionAsync()
    {
        // Tre vincoli prima del token, tutti da soddisfare perché il rosso parli di concorrenza e
        // non di 23502/23503 (pitfall #2620): game_id è NOT NULL con FK verso shared_games,
        // user_id ha FK verso users, e session_code è varchar(6).
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Gioco della sessione",
        });

        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = $"giocatore-{userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });

        var id = Guid.NewGuid();
        _dbContext.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = id,
            UserId = userId,
            GameId = gameId,
            // varchar(6): il codice sessione è corto per essere leggibile a voce al tavolo.
            SessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            SessionType = "Casual",
            Status = "planned",
            SessionDate = DateTime.UtcNow,
            ScoringType = "Points",
            ScoreData = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
        });
        await _dbContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedSessionAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        var seenByFirst = await firstContext.SessionTrackingSessions.FirstAsync(s => s.Id == id);
        var seenBySecond = await secondContext.SessionTrackingSessions.FirstAsync(s => s.Id == id);

        seenByFirst.Status = "in-progress";
        seenByFirst.StartedAt = DateTime.UtcNow;
        await firstContext.SaveChangesAsync();

        // Il secondo partecipante registra i punteggi partendo da una sessione che nel frattempo
        // è stata avviata: senza conflitto rilevato, il suo salvataggio riporta lo stato indietro.
        seenBySecond.ScoreData = """{"alice":12}""";

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due scritture concorrenti sulla stessa sessione devono escludersi: senza conflitto "
            + "rilevato l'ultima vince e il lavoro dell'altro partecipante sparisce (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedSessionAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var session = await context.SessionTrackingSessions.FirstAsync(s => s.Id == id);
        session.ScoreData = """{"bob":7}""";

        var write = async () => await context.SaveChangesAsync();

        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare: se anche questa fallisse, il token sarebbe "
            + "acceso ma il write-path non lo preserverebbe (#3688)");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        (await verification.SessionTrackingSessions.FirstAsync(s => s.Id == id)).ScoreData.Should().Contain("bob");
    }
}
