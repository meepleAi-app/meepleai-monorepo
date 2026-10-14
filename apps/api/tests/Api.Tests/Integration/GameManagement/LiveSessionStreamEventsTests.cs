using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities;
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
/// La fixture di <see cref="LiveSessionStreamEventsTests"/>: host per classe, database per test.
/// </summary>
/// <remarks>
/// Gli eventi di dominio vanno pubblicati in linea (<see cref="DomainEventDispatchMode.Hybrid"/>):
/// il punteggio arriva allo stream attraverso <c>LiveSessionStreamForwarder</c>, un handler di
/// evento di dominio, che con il default <c>OutboxOnly</c> girerebbe in differita.
/// </remarks>
public sealed class LiveSessionStreamEventsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "live_stream_events")
{
    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
                Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }))));
}

/// <summary>
/// #4114 — gli eventi che le pagine toolkit ascoltano, letti da un vero stream
/// <c>GET /api/v1/live-sessions/{id}/stream</c>: nome dell'evento e forma dei dati.
/// </summary>
/// <remarks>
/// La issue chiedeva di verificarli «leggendo un evento reale ricevuto, non assumendo che la
/// connessione aperta basti». Ogni test apre lo stream, aspetta che la sottoscrizione sia
/// registrata (le sottoscrizioni sono lazy: pubblicare prima la perderebbe, vedi #3711), produce
/// l'evento con il comando che lo produce davvero, e legge il frame SSE.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4114")]
public sealed class LiveSessionStreamEventsTests
    : IClassFixture<LiveSessionStreamEventsHostFixture>, IAsyncLifetime
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(20);

    private readonly LiveSessionStreamEventsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public LiveSessionStreamEventsTests(LiveSessionStreamEventsHostFixture hostFixture)
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

    [Fact(DisplayName = "#4114: un punteggio arriva come session:score con playerId, round, dimension e value")]
    public async Task AScore_ArrivesAsSessionScore_WithTheLivePayload()
    {
        var game = await StartGameAsync(withGame: true);
        await using var stream = await OpenStreamAsync(game);
        await WaitForSubscriberAsync(game.TrackingSessionId!.Value);

        await SendAsync(new RecordLiveSessionScoreCommand(game.SessionId, game.MarcoId, 1, "points", 7));

        var data = await ReadEventAsync(stream.Reader, "session:score");
        data.GetProperty("playerId").GetGuid().Should().Be(game.MarcoId);
        data.GetProperty("round").GetInt32().Should().Be(1);
        data.GetProperty("dimension").GetString().Should().Be("points");
        data.GetProperty("value").GetInt32().Should().Be(7);
    }

    [Fact(DisplayName = "#4114: senza companion il punteggio della sessione arriva lo stesso")]
    public async Task WithoutACompanion_TheSessionsOwnScore_StillArrives()
    {
        // Una sessione libera (senza gioco) non ha companion. Prima di #4114 il gateway scartava
        // gli eventi della live in quel caso, e lo stream restava muto su punteggi e stato.
        var game = await StartGameAsync(withGame: false);
        game.TrackingSessionId.Should().BeNull("pre-condizione: una sessione libera non ha companion");
        await using var stream = await OpenStreamAsync(game);
        await WaitForSubscriberAsync(game.SessionId);

        await SendAsync(new RecordLiveSessionScoreCommand(game.SessionId, game.MarcoId, 1, "points", 4));

        var data = await ReadEventAsync(stream.Reader, "session:score");
        data.GetProperty("value").GetInt32().Should().Be(4);
    }

    [Fact(DisplayName = "#4114: l'avanzamento del turno arriva come session:toolkit con i nomi dei giocatori")]
    public async Task ATurnAdvance_ArrivesAsSessionToolkit_WithThePlayerNames()
    {
        // È l'evento che useSessionSync passa a onTurnAdvanced: l'unico che la issue indicava come
        // asimmetrico fra i due stream.
        var game = await StartGameAsync(withGame: true);
        await SendAsync(new InitializeTurnOrderCommand(game.SessionId, ["Marco", "Giulia"]));
        await using var stream = await OpenStreamAsync(game);
        await WaitForSubscriberAsync(game.SessionId);

        await SendAsync(new AdvanceTurnCommand(game.SessionId));

        var data = await ReadEventAsync(stream.Reader, "session:toolkit");
        data.GetProperty("previousPlayerName").GetString().Should().Be("Marco");
        data.GetProperty("currentPlayerName").GetString().Should().Be("Giulia");
        data.GetProperty("roundNumber").GetInt32().Should().BeGreaterThan(0);
    }

    // ─── Aiuti ──────────────────────────────────────────────────────────────────────────────────

    private sealed record StartedGame(
        HttpClient Client, Guid SessionId, Guid MarcoId, Guid? TrackingSessionId);

    private sealed class OpenStream(HttpResponseMessage response, StreamReader reader) : IAsyncDisposable
    {
        public StreamReader Reader { get; } = reader;

        public ValueTask DisposeAsync()
        {
            Reader.Dispose();
            response.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>Crea e avvia una partita con Marco (l'host) e Giulia, con o senza gioco.</summary>
    private async Task<StartedGame> StartGameAsync(bool withGame)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        Guid? gameId = withGame ? await SeedSharedGameAsync(db) : null;
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId, GameName: "Azul", GameId: gameId));
        var marcoId = await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId, DisplayName: "Marco", Color: PlayerColor.Red, UserId: userId));
        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId, DisplayName: "Giulia", Color: PlayerColor.Blue));
        await mediator.Send(new StartLiveSessionCommand(sessionId, userId, UserTier.Free, Role.User));

        var trackingSessionId = await db.LiveGameSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => s.TrackingSessionId)
            .SingleAsync(TestCancellationToken);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        return new StartedGame(client, sessionId, marcoId, trackingSessionId);
    }

    private async Task SendAsync(IBaseRequest command)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestCancellationToken);
    }

    private static async Task<OpenStream> OpenStreamAsync(StartedGame game)
    {
        var response = await game.Client.GetAsync(
            $"/api/v1/live-sessions/{game.SessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            TestCancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStreamAsync(TestCancellationToken);
        return new OpenStream(response, new StreamReader(body));
    }

    /// <summary>
    /// Aspetta che il canale abbia un iscritto: le sottoscrizioni partono alla prima lettura dello
    /// stream, quindi un evento pubblicato prima andrebbe perso.
    /// </summary>
    private async Task WaitForSubscriberAsync(Guid channelId)
    {
        var broadcast = _factory.Services.GetRequiredService<ISessionBroadcastService>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        timeout.CancelAfter(EventTimeout);
        while (broadcast.GetConnectionCount(channelId) == 0)
            await Task.Delay(50, timeout.Token);
    }

    /// <summary>Legge i frame SSE fino al primo evento di quel tipo, e ne restituisce i dati.</summary>
    private static async Task<JsonElement> ReadEventAsync(StreamReader reader, string eventType)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        timeout.CancelAfter(EventTimeout);
        string? currentType = null;
        while (true)
        {
            var line = await reader.ReadLineAsync(timeout.Token)
                ?? throw new InvalidOperationException($"lo stream si è chiuso prima di un evento {eventType}");
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentType = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal) && currentType == eventType)
            {
                using var json = JsonDocument.Parse(line["data: ".Length..]);
                return json.RootElement.Clone();
            }
        }
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"live-stream-{userId:N}@test.local",
            DisplayName = "Live Stream Test User",
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
            Title = $"Live Stream Test Game {gameId:N}",
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
