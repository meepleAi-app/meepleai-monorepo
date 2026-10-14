using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// La fixture di <see cref="LiveSessionScoringScenarioTests"/>: host per classe, database per test.
/// </summary>
/// <remarks>
/// Gli eventi di dominio vanno pubblicati in linea (<see cref="DomainEventDispatchMode.Hybrid"/>):
/// con il default <c>OutboxOnly</c> la partita registrata la scrive il processore dell'outbox, in
/// differita, e il test leggerebbe una tabella ancora vuota.
/// </remarks>
public sealed class LiveSessionScoringScenarioHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "live_scoring")
{
    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
                Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }))));
}

/// <summary>
/// #4156 fetta 2 — il percorso completo di una partita creata dal wizard, sulle rotte che la
/// scheda Score usa: crea (Marco, Giulia) → segna → ricarica → chiudi → la partita registrata
/// <b>esiste</b> e porta gli stessi totali.
/// </summary>
/// <remarks>
/// La generazione della partita registrata (<c>LiveSessionCompletedEventHandler</c>) inghiotte i
/// propri errori, quindi questo scenario è l'unica guardia sulla sua esistenza. Prima della
/// correzione falliva in due modi, entrambi silenziosi: con la dimensione del wizard («Punti») la
/// partita registrata non veniva scritta, e su più round portava l'ultimo round invece del totale.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4156")]
public sealed class LiveSessionScoringScenarioTests
    : IClassFixture<LiveSessionScoringScenarioHostFixture>, IAsyncLifetime
{
    private const string Dimension = "Punti";

    private readonly LiveSessionScoringScenarioHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public LiveSessionScoringScenarioTests(LiveSessionScoringScenarioHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact(DisplayName = "#4156: segna 10 e 30, ricarica, chiudi — la partita registrata esiste con gli stessi totali")]
    public async Task Scenario_TheScoresSurviveAReload_AndThePlayRecordCarriesTheTotals()
    {
        var game = await StartWizardGameAsync();

        (await RecordAsync(game, HttpMethod.Post, game.MarcoId, round: 1, value: 10))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RecordAsync(game, HttpMethod.Post, game.GiuliaId, round: 1, value: 30))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Ricarica: i totali sono quelli che il backend ha ricalcolato, e Giulia è in testa.
        var reloaded = await ReadPlayersAsync(game);
        reloaded["Marco"].Total.Should().Be(10);
        reloaded["Giulia"].Total.Should().Be(30);
        reloaded["Giulia"].Rank.Should().Be(1);
        reloaded["Marco"].Rank.Should().Be(2);

        (await game.Client.PostAsync($"/api/v1/live-sessions/{game.SessionId}/complete", content: null, TestCancellationToken))
            .IsSuccessStatusCode.Should().BeTrue();

        var record = await ReadPlayRecordScoresAsync(game.GameId);
        record.Should().NotBeNull("chiudere la partita deve produrre la sua partita registrata");
        record!["Marco"].Should().Be(10);
        record["Giulia"].Should().Be(30);
    }

    [Fact(DisplayName = "#4156: su due round, con una correzione, la partita registrata porta i totali")]
    public async Task Scenario_OverTwoRoundsWithACorrection_ThePlayRecordCarriesTheTotals()
    {
        var game = await StartWizardGameAsync();

        (await RecordAsync(game, HttpMethod.Post, game.MarcoId, round: 1, value: 10)).EnsureSuccessStatusCode();
        // La correzione di una cella già scritta passa da PUT e la sostituisce.
        (await RecordAsync(game, HttpMethod.Put, game.MarcoId, round: 1, value: 12)).EnsureSuccessStatusCode();
        (await RecordAsync(game, HttpMethod.Post, game.MarcoId, round: 2, value: 5)).EnsureSuccessStatusCode();
        (await RecordAsync(game, HttpMethod.Post, game.GiuliaId, round: 1, value: 30)).EnsureSuccessStatusCode();

        var reloaded = await ReadPlayersAsync(game);
        reloaded["Marco"].Total.Should().Be(17);
        reloaded["Giulia"].Total.Should().Be(30);

        (await game.Client.PostAsync($"/api/v1/live-sessions/{game.SessionId}/complete", content: null, TestCancellationToken))
            .IsSuccessStatusCode.Should().BeTrue();

        var record = await ReadPlayRecordScoresAsync(game.GameId);
        record.Should().NotBeNull();
        record!["Marco"].Should().Be(17, "la partita registrata porta il totale dei round, non l'ultimo");
        record["Giulia"].Should().Be(30);
    }

    // ─── Aiuti ──────────────────────────────────────────────────────────────────────────────────

    private sealed record WizardGame(
        HttpClient Client, Guid SessionId, Guid GameId, Guid MarcoId, Guid GiuliaId);

    private sealed record PlayerTotal(int Total, int Rank);

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Come il wizard desktop: crea la partita sul gioco con la sua dimensione predefinita
    /// («Punti», in PV), aggiunge Marco (l'host) e Giulia (un'ospite), avvia.
    /// </summary>
    private async Task<WizardGame> StartWizardGameAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Azul",
            GameId: gameId,
            ScoringDimensions: [Dimension],
            DimensionUnits: new Dictionary<string, string> { [Dimension] = "PV" }));
        var marcoId = await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId, DisplayName: "Marco", Color: PlayerColor.Red, UserId: userId));
        var giuliaId = await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId, DisplayName: "Giulia", Color: PlayerColor.Blue));
        await mediator.Send(new StartLiveSessionCommand(sessionId, userId, UserTier.Free, Role.User));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        return new WizardGame(client, sessionId, gameId, marcoId, giuliaId);
    }

    /// <summary>Scrive una cella come fa la scheda Score: POST per una cella nuova, PUT per correggerla.</summary>
    private static Task<HttpResponseMessage> RecordAsync(
        WizardGame game, HttpMethod method, Guid playerId, int round, int value)
    {
        var request = new HttpRequestMessage(method, $"/api/v1/live-sessions/{game.SessionId}/scores")
        {
            Content = JsonContent.Create(new { playerId, round, dimension = Dimension, value, unit = "PV" }),
        };
        return game.Client.SendAsync(request, TestCancellationToken);
    }

    /// <summary>Rilegge la live via HTTP, come una pagina ricaricata: totale e posizione per nome.</summary>
    private static async Task<Dictionary<string, PlayerTotal>> ReadPlayersAsync(WizardGame game)
    {
        var response = await game.Client.GetAsync($"/api/v1/live-sessions/{game.SessionId}", TestCancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestCancellationToken));
        return json.RootElement.GetProperty("players").EnumerateArray().ToDictionary(
            p => p.GetProperty("displayName").GetString()!,
            p => new PlayerTotal(p.GetProperty("totalScore").GetInt32(), p.GetProperty("currentRank").GetInt32()));
    }

    /// <summary>
    /// La partita registrata del gioco, con il punteggio di ciascun giocatore sulla dimensione;
    /// <c>null</c> se non esiste.
    /// </summary>
    private async Task<Dictionary<string, int>?> ReadPlayRecordScoresAsync(Guid gameId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await db.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players).ThenInclude(p => p.Scores)
            .SingleOrDefaultAsync(r => r.GameId == gameId, TestCancellationToken);
        return record?.Players.ToDictionary(
            p => p.DisplayName,
            p => p.Scores.Single(s => string.Equals(s.Dimension, Dimension, StringComparison.OrdinalIgnoreCase)).Value);
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"live-scoring-{userId:N}@test.local",
            DisplayName = "Live Scoring Test User",
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
            Title = $"Live Scoring Test Game {gameId:N}",
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
}
