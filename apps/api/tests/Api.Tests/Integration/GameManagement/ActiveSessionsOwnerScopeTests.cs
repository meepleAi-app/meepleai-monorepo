using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// #4050. Host condiviso per la classe, database fresco per test.
/// </summary>
public sealed class ActiveSessionsOwnerScopeTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "active_owner_scope");

/// <summary>
/// Issue #4080 — <c>GetActiveSessionsQuery</c> deve restituire SOLO le sessioni del chiamante.
///
/// <para><b>Il difetto che questi test presidiano.</b> Prima della correzione la query non
/// portava alcuna identità: <c>GET /api/v1/sessions/active</c> consegnava le sessioni attive di
/// <i>tutti</i> a qualunque account autenticato. Misurato su stack locale con un account creato
/// dieci secondi prima: libreria vuota, zero sessioni proprie, e in risposta quattro sessioni di
/// altri due utenti con i loro <c>playerName</c>, <c>notes</c> e <c>scoreData</c>.</para>
///
/// <para>🔴 <b>Perché di integrazione e non unit.</b> I test unit dell'handler
/// (<c>GetActiveSessionsQueryHandlerTests</c>) montano un <c>Mock&lt;IGameSessionRepository&gt;</c>:
/// un mock restituisce ciò che gli si dice, quindi là si può verificare che l'handler
/// <i>propaghi</i> il proprietario, mai che il filtro esista. L'unico posto dove «filtrato»
/// è un'affermazione falsificabile è contro il database.</para>
///
/// <para>⚠️ <b>Perché tre asserzioni e non una.</b> Con il solo caso «utente senza sessioni
/// vede zero», un repository che ritornasse sempre vuoto passerebbe il test e romperebbe il
/// prodotto. Serve anche il caso positivo (l'utente vede le proprie) e il caso avversario (la
/// sessione di un altro utente ESISTE nello stesso database e NON compare). Il terzo caso —
/// sessioni senza proprietario — esiste perché <c>CreatedByUserId</c> è nullable: non va
/// dedotto, va asserito.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4080")]
public sealed class ActiveSessionsOwnerScopeTests
    : IClassFixture<ActiveSessionsOwnerScopeTestsHostFixture>, IAsyncLifetime
{
    private readonly ActiveSessionsOwnerScopeTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public ActiveSessionsOwnerScopeTests(ActiveSessionsOwnerScopeTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // #4050: DEVE precedere ogni uso di _factory, altrimenti il seed finisce nel database
        // di avvio e la query legge quello per test.
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ──────────────────────────────────────────────────────────────────────────
    // Il caso avversario, ed è quello che falliva prima della correzione: la
    // sessione dell'altro utente esiste nello stesso database, quindi un filtro
    // assente la farebbe comparire.
    // ──────────────────────────────────────────────────────────────────────────
    [Fact(DisplayName = "#4080: la query non restituisce la sessione attiva di un altro utente")]
    public async Task Handle_WithAnotherUsersActiveSession_DoesNotReturnIt()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await SeedSharedGameAsync(db);
        var caller = await SeedUserAsync(db, "caller");
        var other = await SeedUserAsync(db, "other");

        var mine = await SeedActiveSessionAsync(db, gameId, caller, notes: "note del chiamante");
        var theirs = await SeedActiveSessionAsync(db, gameId, other, notes: "note di un altro utente");

        var result = await mediator.Send(new GetActiveSessionsQuery(caller));

        result.Sessions.Select(s => s.Id).Should().Contain(mine,
            because: "la sessione del chiamante deve comparire: senza questa asserzione un " +
                     "repository che ritorna sempre vuoto passerebbe il test");

        result.Sessions.Select(s => s.Id).Should().NotContain(theirs,
            because: "#4080 — la sessione di un altro utente esiste in questo database e non " +
                     "deve essere visibile al chiamante");

        result.Sessions.Should().HaveCount(1);

        // Il totale paginato va filtrato insieme alla pagina: un conteggio globale annuncerebbe
        // pagine irraggiungibili e rivelerebbe quante sessioni esistono in tutto.
        result.Total.Should().Be(1,
            because: "Total deve contare le sessioni del chiamante, non quelle globali");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Il caso del primo accesso, che è il sintomo per cui l'issue è stata aperta.
    // ──────────────────────────────────────────────────────────────────────────
    [Fact(DisplayName = "#4080: un utente senza sessioni ne vede zero, anche se altre esistono")]
    public async Task Handle_WithFreshUser_ReturnsEmpty_EvenWhenOtherSessionsExist()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await SeedSharedGameAsync(db);
        var other = await SeedUserAsync(db, "other");
        await SeedActiveSessionAsync(db, gameId, other, notes: "non deve uscire");

        var fresh = await SeedUserAsync(db, "fresh");

        var result = await mediator.Send(new GetActiveSessionsQuery(fresh));

        result.Sessions.Should().BeEmpty(
            because: "è il sintomo di #4080: un account appena creato riceveva le sessioni di tutti");
        result.Total.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // `CreatedByUserId` è nullable: le sessioni senza proprietario esistono e non
    // appartengono a nessuno. `== ownerUserId` le esclude, ed è la direzione
    // voluta — ma va asserita, non dedotta dal tipo.
    // ──────────────────────────────────────────────────────────────────────────
    [Fact(DisplayName = "#4080: una sessione senza proprietario non viene restituita a nessuno")]
    public async Task Handle_WithOwnerlessActiveSession_ReturnsItToNobody()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await SeedSharedGameAsync(db);
        var caller = await SeedUserAsync(db, "caller");
        var ownerless = await SeedActiveSessionAsync(db, gameId, ownerUserId: null, notes: "senza proprietario");

        var result = await mediator.Send(new GetActiveSessionsQuery(caller));

        result.Sessions.Select(s => s.Id).Should().NotContain(ownerless,
            because: "CreatedByUserId NULL significa 'di nessuno', non 'di tutti'");
        result.Sessions.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fail-closed: un proprietario vuoto non deve diventare «nessun filtro».
    // ──────────────────────────────────────────────────────────────────────────
    [Fact(DisplayName = "#4080: OwnerUserId vuoto viene rifiutato invece di non filtrare")]
    public async Task Handle_WithEmptyOwner_Throws_RatherThanQueryingUnscoped()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await SeedSharedGameAsync(db);
        var other = await SeedUserAsync(db, "other");
        await SeedActiveSessionAsync(db, gameId, other, notes: "non deve uscire da un Guid.Empty");

        var act = async () => await mediator.Send(new GetActiveSessionsQuery(Guid.Empty));

        await act.Should().ThrowAsync<ArgumentException>(
            because: "un Guid.Empty che scivolasse fino al repository equivarrebbe a non filtrare");
    }

    // ── helper ────────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db, string label)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"owner-scope-{label}-{userId:N}@test.local",
            DisplayName = $"Owner Scope {label}",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return userId;
    }

    private static async Task<Guid> SeedSharedGameAsync(MeepleAiDbContext db)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Owner Scope Test Game {gameId:N}",
            YearPublished = 2024,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return gameId;
    }

    private static async Task<Guid> SeedActiveSessionAsync(
        MeepleAiDbContext db,
        Guid gameId,
        Guid? ownerUserId,
        string notes)
    {
        var sessionId = Guid.NewGuid();

        // 🔴 `PlayersJson = "[]"` NON va bene, e il modo in cui l'ho scoperto vale la nota:
        // `GameSession`.ctor pretende almeno un giocatore, quindi `MapToDomain` lancia
        // `ArgumentException: Session must have at least one player` — ma solo quando una
        // sessione viene davvero mappata. Con i soli test che si aspettano zero risultati il
        // seed rotto restava invisibile: è il caso positivo che lo ha fatto emergere.
        //
        // Serializzato col DTO e il serializzatore della produzione invece che con un letterale
        // JSON, così il seed segue la convenzione dei nomi se un giorno cambia.
        var playersJson = JsonSerializer.Serialize(new List<SessionPlayerDto>
        {
            new(PlayerName: "Tester", PlayerOrder: 1, Color: null)
        });

        db.GameSessions.Add(new GameSessionEntity
        {
            Id = sessionId,
            GameId = gameId,
            CreatedByUserId = ownerUserId,
            // "InProgress" è uno dei tre stati che FindActiveAsync considera attivi.
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            Notes = notes,
            PlayersJson = playersJson
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return sessionId;
    }
}
