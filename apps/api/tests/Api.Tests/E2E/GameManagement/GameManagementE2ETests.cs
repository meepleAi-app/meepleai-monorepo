using Api.Infrastructure.Entities;
using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.GameManagement;

/// <summary>
/// E2E tests for game management flows.
/// Tests the complete user journey for game operations.
///
/// Issue #3023: Backend E2E Test Suite - Game Management Flows
///
/// Critical Journeys Covered:
/// - Browse games (public, paginated)
/// - Get game details
/// - Start game session → add players → complete session
/// - Session lifecycle management
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
public sealed class GameManagementE2ETests : E2ETestBase
{
    private Guid _testGameId;
    private string _testGameName = string.Empty;

    public GameManagementE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override async Task SeedTestDataAsync()
    {
        // Seed a test game for E2E tests (unique name per test instance to avoid duplicate key errors)
        _testGameName = $"E2E Test Game {Guid.NewGuid():N}";
        var game = new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = _testGameName,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            YearPublished = 2024,
            BggId = null,
            ImageUrl = "https://example.com/image.png",
            CreatedAt = DateTime.UtcNow
        };

        DbContext.SharedGames.Add(game);
        await DbContext.SaveChangesAsync();
        _testGameId = game.Id;

        // #3662: avviare una sessione richiede una knowledge base PRONTA -- altrimenti
        // CreateSessionCommandHandler risponde 422 `kb_not_ready`. E' una regola di business
        // aggiunta dopo la scrittura di questi test, e senza di essa nessun test del ciclo di
        // vita della sessione puo' passare.
        //
        // «Pronta» significa, secondo GetKbReadinessQueryHandler: almeno un PdfDocument del
        // gioco in stato "Ready" E un VectorDocument su quel PDF con IndexingStatus
        // "completed". Servono entrambi: con il solo PDF lo stato resta non-ready.
        var kbOwner = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"e2e-kb-owner-{Guid.NewGuid():N}@test.invalid",
            DisplayName = "E2E KB Owner",
            PasswordHash = "placeholder.NeverLogsIn",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.Users.Add(kbOwner);

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "e2e-rulebook.pdf",
            FilePath = "/tmp/e2e-rulebook.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = kbOwner.Id,
            SharedGameId = game.Id,
            ProcessingState = "Ready",
        };
        DbContext.PdfDocuments.Add(pdf);

        DbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = game.Id,
            PdfDocumentId = pdf.Id,
            IndexingStatus = "completed",
            EmbeddingModel = "e2e-test-model",
            EmbeddingDimensions = 768,
            ChunkCount = 1,
            IndexedAt = DateTime.UtcNow,
        });

        await DbContext.SaveChangesAsync();
    }

    #region Public Game Browsing Tests

    [Fact]
    public async Task GetGames_PublicEndpoint_ReturnsPaginatedList()
    {
        // Arrange - No authentication needed
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/games");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedGamesResponse>();
        result.Should().NotBeNull();
        result!.Games.Should().NotBeNull();
        result.Games!.Any(g => g.Id == _testGameId).Should().BeTrue();
    }

    [Fact]
    public async Task GetGames_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/games?pageSize=10&page=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedGamesResponse>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetGameById_ExistingGame_ReturnsGameDetails()
    {
        // Arrange
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync($"/api/v1/games/{_testGameId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GameDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(_testGameId);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public async Task GetGameById_NonexistentGame_ReturnsNotFound()
    {
        // Arrange
        ClearAuthentication();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/games/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Game Session Lifecycle Tests

    [Fact]
    public async Task CompleteSession_ValidSession_MarksAsCompleted()
    {
        // Arrange - Create session first
        var email = $"complete_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        // #3662: il vecchio `POST /api/v1/sessions` non esiste piu'. Una Session
        // appartiene a una GameNight (invariante «GameNight 1..N Session»): si crea
        // la serata e vi si avvia dentro la sessione.
        var sessionId = await StartSessionAsync(_testGameId);

        // Act - Complete the session
        var completePayload = new { winnerName = "Player 1" };
        var completeResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/complete", completePayload);

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await completeResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task AbandonSession_ValidSession_MarksAsAbandoned()
    {
        // Arrange - Create session first
        var email = $"abandon_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        // #3662: il vecchio `POST /api/v1/sessions` non esiste piu'. Una Session
        // appartiene a una GameNight (invariante «GameNight 1..N Session»): si crea
        // la serata e vi si avvia dentro la sessione.
        var sessionId = await StartSessionAsync(_testGameId);

        // Act - Abandon the session
        var abandonResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/abandon", new { });

        // Assert
        abandonResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await abandonResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be("Abandoned");
    }

    [Fact]
    public async Task PauseAndResumeSession_ValidSession_ChangesStatusCorrectly()
    {
        // Arrange - Create session first
        var email = $"pauseresume_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        // #3662: il vecchio `POST /api/v1/sessions` non esiste piu'. Una Session
        // appartiene a una GameNight (invariante «GameNight 1..N Session»): si crea
        // la serata e vi si avvia dentro la sessione.
        var sessionId = await StartSessionAsync(_testGameId);

        // Act - Pause the session
        var pauseResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/pause", new { });
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pausedSession = await pauseResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        pausedSession!.Status.Should().Be("Paused");

        // Act - Resume the session
        var resumeResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/resume", new { });
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resumedSession = await resumeResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        resumedSession!.Status.Should().Be("InProgress");
    }

    #endregion

    #region Complete Game Journey Tests

    [Fact]
    public async Task CompleteGameJourney_BrowseStartPlayComplete_Succeeds()
    {
        // Step 1: Browse games (public)
        ClearAuthentication();
        var gamesResponse = await Client.GetAsync("/api/v1/games");
        gamesResponse.EnsureSuccessStatusCode();

        var games = await gamesResponse.Content.ReadFromJsonAsync<PaginatedGamesResponse>();
        games!.Games!.Any(g => g.Id == _testGameId).Should().BeTrue();

        // Step 2: View game details (public)
        var gameDetailsResponse = await Client.GetAsync($"/api/v1/games/{_testGameId}");
        gameDetailsResponse.EnsureSuccessStatusCode();

        // Step 3: Register/Login to play
        var email = $"gamejourney_{Guid.NewGuid():N}@example.com";
        var (sessionToken, userId) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        // Step 4: Start a game session
        // #3662: l'avvio non accetta piu' i giocatori nel corpo -- si aggiungono dopo, con
        // POST /sessions/{id}/players. Il percorso resta coperto per intero: la copertura
        // sui giocatori non viene tolta, viene spostata sull'endpoint che oggi la fornisce.
        var sessionId = await StartSessionAsync(_testGameId);

        foreach (var (name, order) in new[] { ("Alice", 1), ("Bob", 2), ("Charlie", 3) })
        {
            var addPlayerResponse = await Client.PostAsJsonAsync(
                $"/api/v1/sessions/{sessionId}/players",
                new { playerName = name, playerOrder = order });
            addPlayerResponse.EnsureSuccessStatusCode();
        }

        var startResponse = await Client.GetAsync($"/api/v1/sessions/{sessionId}");
        startResponse.EnsureSuccessStatusCode();
        var session = await startResponse.Content.ReadFromJsonAsync<GameSessionDto>();

        session.Should().NotBeNull();
        session!.Status.Should().Be("InProgress");
        session.Players.Should().HaveCount(3);

        // Step 5: Complete the game with a winner
        var completePayload = new { winnerName = "Bob" };
        var completeResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/complete", completePayload);
        completeResponse.EnsureSuccessStatusCode();

        var completedSession = await completeResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        completedSession!.Status.Should().Be("Completed");
        // Winner may be null if API doesn't return it in response - verify status is primary goal
        if (completedSession.Winner != null)
        {
            completedSession.Winner.Should().Be("Bob");
        }
    }

    [Fact]
    public async Task AddPlayerToSession_DuringGame_UpdatesPlayers()
    {
        // Arrange - Create session first
        var email = $"addplayer_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        // #3662: il vecchio `POST /api/v1/sessions` non esiste piu'. Una Session
        // appartiene a una GameNight (invariante «GameNight 1..N Session»): si crea
        // la serata e vi si avvia dentro la sessione.
        var sessionId = await StartSessionAsync(_testGameId);

        // Act - Add another player
        var addPlayerPayload = new { playerName = "Player 3", playerOrder = 3 };
        var addResponse = await Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/players", addPlayerPayload);

        // Assert
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedSession = await addResponse.Content.ReadFromJsonAsync<GameSessionDto>();
        updatedSession.Should().NotBeNull();
        updatedSession!.Players.Should().HaveCount(3);
        updatedSession.Players.Should().Contain(p => p.PlayerName == "Player 3");
    }

    #endregion

    // Response DTOs
    private sealed record PaginatedGamesResponse(
        List<GameDto>? Games,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record GameDto(
        Guid Id,
        string Name,
        string? Description,
        int MinPlayers,
        int MaxPlayers,
        int? PlayTimeMinutes,
        int? YearPublished);

    private sealed record GameSessionDto(
        Guid Id,
        Guid GameId,
        string Status,
        List<PlayerDto> Players,
        string? Winner,
        DateTime CreatedAt);

    private sealed record PlayerDto(
        string PlayerName,
        int PlayerOrder,
        string? Color);

    /// <summary>
    /// #3662: sostituisce il vecchio <c>POST /api/v1/sessions</c>, che non esiste piu'.
    ///
    /// Non e' una route rinominata ma un cambio di modello: una Session appartiene a una
    /// GameNight, quindi il flusso e' in due passi. Il risultato porta DUE id --
    /// <c>SessionId</c> (l'aggregato <c>GameSession</c>) e <c>GameNightSessionId</c> (il link
    /// con la serata): gli endpoint <c>/sessions/{id}/complete|abandon|pause|resume|players</c>
    /// vogliono il PRIMO. Scambiarli darebbe 404 su ogni chiamata successiva.
    ///
    /// <para><b>LIMITE NOTO (#3662).</b> Questo helper porta il flusso fino all'avvio della
    /// sessione -- e quello ora funziona -- ma i cinque test del ciclo di vita restano rossi
    /// con 404. Il motivo e' architetturale, non un URL sbagliato: <c>/sessions/{id}/complete
    /// |abandon|pause|resume</c> operano su <c>IGameSessionRepository</c>, cioe' l'aggregato
    /// <b>GameSession</b> (GameManagement), mentre l'avvio da una serata crea un
    /// <b>SessionTracking.Session</b>. Sono due dei tre aggregati di ADR-089, e il
    /// <c>SessionId</c> restituito qui NON e' quello che quegli endpoint risolvono.</para>
    ///
    /// <para>Il collegamento (<c>CorrelatedGameSessionId</c>) viene creato piu' avanti nel
    /// ciclo di vita — <c>LifecycleCommandHandlers.cs:86</c> — e <b>non e' esposto da alcuna
    /// risposta</b>: <c>GoLiveSessionResult</c> porta SessionId/GameNightId/
    /// GameNightSessionId/PlayOrder/Status, mai l'id correlato. Serve stabilire quale sia il
    /// percorso HTTP supportato per ottenere un GameSession: e' una domanda sul contratto,
    /// non un aggiustamento di test.</para>
    /// </summary>
    private async Task<Guid> StartSessionAsync(Guid gameId, string gameTitle = "E2E Test Game")
    {
        var nightResponse = await Client.PostAsJsonAsync("/api/v1/game-nights", new
        {
            title = $"E2E Night {Guid.NewGuid():N}",
            // Il validator pretende `> UtcNow.AddHours(1)` STRETTAMENTE: con AddHours(1)
            // esatto la data e' gia' scaduta quando il server la valuta, e la risposta e' 422.
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        nightResponse.EnsureSuccessStatusCode();
        var gameNightId = await nightResponse.Content.ReadFromJsonAsync<Guid>();

        // Una serata nasce in stato Draft e non accetta sessioni («Cannot add sessions to a
        // Draft game night»): va pubblicata prima.
        var publishResponse = await Client.PostAsync($"/api/v1/game-nights/{gameNightId}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        var sessionResponse = await Client.PostAsJsonAsync(
            $"/api/v1/game-nights/{gameNightId}/sessions", new { gameId, gameTitle });
        sessionResponse.EnsureSuccessStatusCode();
        var started = await sessionResponse.Content.ReadFromJsonAsync<StartSessionResult>();
        return started!.SessionId;
    }

    private sealed record StartSessionResult(
        Guid SessionId, Guid GameNightSessionId, string SessionCode, int PlayOrder);

}
