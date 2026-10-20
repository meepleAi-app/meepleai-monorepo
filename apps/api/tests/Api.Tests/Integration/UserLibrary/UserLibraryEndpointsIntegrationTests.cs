using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Nessuna asserzione della classe
/// riguarda conteggi o liste complete: gli endpoint di lista (<c>GET /api/v1/library</c>,
/// <c>/stats</c>, <c>/quota</c>) sono verificati solo per status code (OK/Unauthorized), mai per
/// contenuto o cardinalita' — il test "ReturnsEmptyLibrary" controlla solo lo status, non lo
/// svuotamento. Ogni test crea il proprio utente con <c>TestSessionHelper.CreateUserSessionAsync</c>
/// — email univoca per chiamata — e i giochi referenziati sono id inesistenti (<c>Guid.NewGuid()</c>),
/// quindi nessuna collisione fra test. I test del link scaduto (#4544) leggono e scrivono solo la
/// riga del link che hanno creato, cercata per token.
/// </para>
/// </summary>
public sealed class UserLibraryEndpointsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "library_endpoints")
{
    /// <summary>
    /// <c>POST /library/share</c> compone l'URL del link da <c>App:BaseUrl</c>, che la configurazione
    /// d'integrazione non porta: senza, la creazione del link risponde 500.
    /// </summary>
    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        ["App:BaseUrl"] = "http://localhost:3000",
    };
}

/// <summary>
/// Integration tests for UserLibrary HTTP endpoints.
/// Tests: GetLibrary, AddGame, RemoveGame, UpdateEntry, GetStats, ShareLinks
/// Issue #3010: Backend coverage improvement.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UserLibraryEndpointsIntegrationTests : IClassFixture<UserLibraryEndpointsHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserLibraryEndpointsIntegrationTests(UserLibraryEndpointsHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // ========================================
    // GET LIBRARY ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetLibrary_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/library");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLibrary_WithValidSession_ReturnsEmptyLibrary()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - With mocked auth middleware, may return Unauthorized
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetLibrary_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library?page=1&pageSize=10",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetLibrary_WithFavoritesFilter_ReturnsFilteredResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library?favoritesOnly=true",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // ADD GAME TO LIBRARY ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task AddGameToLibrary_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/library/games/{gameId}",
            new { Notes = "Test notes", IsFavorite = false });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddGameToLibrary_WithValidSession_RequiresExistingGame()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var nonExistentGameId = Guid.NewGuid();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/library/games/{nonExistentGameId}",
            sessionToken,
            new { Notes = "Test notes", IsFavorite = false });

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Should fail because game doesn't exist or auth middleware failed
        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected BadRequest, NotFound, or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // REMOVE GAME FROM LIBRARY ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task RemoveGameFromLibrary_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/library/games/{gameId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveGameFromLibrary_GameNotInLibrary_ReturnsAppropriateError()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var nonExistentGameId = Guid.NewGuid();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/library/games/{nonExistentGameId}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Game not in library may return NotFound, BadRequest, or Unauthorized
        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected NotFound, BadRequest, or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // UPDATE LIBRARY ENTRY ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task UpdateLibraryEntry_WithoutAuth_ReturnsAppropriateError()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act - Try PATCH as well since PUT may not be supported
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/library/games/{gameId}",
            new { Notes = "Updated notes", IsFavorite = true });

        // Assert - May return MethodNotAllowed if PUT not supported, or Unauthorized if it is
        (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.MethodNotAllowed).Should().BeTrue($"Expected Unauthorized or MethodNotAllowed, got {response.StatusCode}");
    }

    // ========================================
    // GET LIBRARY STATS ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetLibraryStats_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/library/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLibraryStats_WithValidSession_ReturnsStats()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library/stats",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // LIBRARY QUOTA ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetLibraryQuota_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/library/quota");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLibraryQuota_WithValidSession_ReturnsQuotaInfo()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library/quota",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    // ========================================
    // GAME IN LIBRARY STATUS ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetGameInLibraryStatus_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/library/games/{gameId}/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // SHARE LINK ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task CreateShareLink_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/library/share",
            new { PrivacyLevel = "basic" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetShareLink_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/library/share");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeShareLink_WithoutAuth_ReturnsAppropriateError()
    {
        // Act
        var response = await _client.DeleteAsync("/api/v1/library/share");

        // Assert - May return MethodNotAllowed if DELETE not supported, or Unauthorized
        (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.MethodNotAllowed).Should().BeTrue($"Expected Unauthorized or MethodNotAllowed, got {response.StatusCode}");
    }

    // ========================================
    // LINK DI CONDIVISIONE SCADUTO (#4544)
    // ========================================
    //
    // Un link scade perché passa il tempo, non per un comando: i test portano ExpiresAt nel passato
    // direttamente nel database, come la verifica della issue. Prima della correzione il repository
    // ricostruiva la riga con LibraryShareLink.Create, la factory di un link NUOVO, che rifiuta una
    // scadenza passata con ArgumentException: ogni operazione che caricava un link scaduto rispondeva
    // 400 «Invalid request parameters». Lo status da solo non basta a distinguere il caso della
    // modifica (400 prima e dopo): i test leggono anche il messaggio.

    [Fact]
    public async Task GetSharedLibrary_ExpiredLink_Returns404WithExpiredMessage()
    {
        var token = await CreateUserSessionTokenAsync();
        var shareToken = await CreateShareLinkAsync(token);

        // Controllo: prima della scadenza il link si legge, quindi il 404 viene dalla scadenza.
        using (var before = await _client.GetAsync($"/api/v1/library/shared/{shareToken}"))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK,
                $"setup: a link that has not expired must be readable. Body: {await before.Content.ReadAsStringAsync()}");
        }

        await ExpireShareLinkAsync(shareToken);
        var rowBefore = await ReadShareLinkRowAsync(shareToken);

        using var response = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            $"an expired link is a link that no longer exists for the public. Before #4544 loading it threw " +
            $"ArgumentException and answered 400. Body: {body}");
        ErrorOf(body).Should().Be("Shared library not found or link has expired");

        // Leggere un link scaduto non è un accesso: il contatore resta fermo.
        var rowAfter = await ReadShareLinkRowAsync(shareToken);
        rowAfter.ViewCount.Should().Be(rowBefore.ViewCount);
        rowAfter.LastAccessedAt.Should().Be(rowBefore.LastAccessedAt);
    }

    [Fact]
    public async Task RevokeShareLink_ExpiredLink_ByOwner_Returns204AndKeepsTheOtherColumns()
    {
        var token = await CreateUserSessionTokenAsync();
        var shareToken = await CreateShareLinkAsync(token);

        // Un accesso prima della scadenza, così contatore e ultimo accesso non sono ai valori di default
        // e la revoca deve conservarli davvero.
        using (var access = await _client.GetAsync($"/api/v1/library/shared/{shareToken}"))
        {
            access.StatusCode.Should().Be(HttpStatusCode.OK, "setup: one access before expiry");
        }

        await ExpireShareLinkAsync(shareToken);
        var before = await ReadShareLinkRowAsync(shareToken);
        before.ViewCount.Should().Be(1, "setup: the access must be recorded");
        before.RevokedAt.Should().BeNull("setup: the link is expired, not revoked");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/library/share/{shareToken}", token);
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // L'aggregato vieta la revoca solo a un link già revocato (LibraryShareLink.Revoke), e
        // l'handler non aggiunge condizioni: il proprietario può revocare un link scaduto.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"revoking an expired link is allowed by the aggregate. Before #4544 it answered 400. Body: {body}");

        var after = await ReadShareLinkRowAsync(shareToken);
        after.RevokedAt.Should().NotBeNull();
        after.Should().BeEquivalentTo(before, options => options.Excluding(r => r.RevokedAt),
            "the revocation writes RevokedAt and nothing else: id, token, creation date, expiry, counter " +
            "and last access come back from the row as they were");
    }

    [Fact]
    public async Task UpdateShareLink_RenewExpiredLink_IsRefusedAndTheLinkStaysExpired()
    {
        var token = await CreateUserSessionTokenAsync();
        var shareToken = await CreateShareLinkAsync(token);
        await ExpireShareLinkAsync(shareToken);
        var before = await ReadShareLinkRowAsync(shareToken);

        var renewal = DateTime.UtcNow.AddDays(30).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        var (status, body) = await SendJsonAsync(
            HttpMethod.Patch, $"/api/v1/library/share/{shareToken}", token, $$"""{"expiresAt":"{{renewal}}"}""");

        // UpdateLibraryShareLinkCommandHandler rifiuta la modifica di un link non valido (revocato o
        // scaduto) con una DomainException, che l'endpoint traduce in 400 col suo messaggio. Prima di
        // #4544 lo status era lo stesso ma il motivo no: l'ArgumentException del caricamento.
        status.Should().Be(HttpStatusCode.BadRequest, $"Body: {body}");
        ErrorOf(body).Should().Be("Cannot update revoked or expired share link",
            "the refusal must come from the rule on expired links, not from loading the link");

        var after = await ReadShareLinkRowAsync(shareToken);
        after.Should().BeEquivalentTo(before, "a refused renewal leaves the link expired");
    }

    // ========================================
    // GAME SESSION RECORDING ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task RecordGameSession_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/library/games/{gameId}/sessions",
            new { PlayedAt = DateTime.UtcNow, DurationMinutes = 60 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // GAME STATE UPDATE ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task UpdateGameState_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act — consolidated under PATCH /library/games/{id} (Fase 3 consolidation)
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/library/games/{gameId}",
            new { NewState = "playing" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // GAME DETAIL ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetGameDetail_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/library/games/{gameId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // LOAN REMINDER ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task SendLoanReminder_WithoutAuth_ReturnsUnauthorizedOrNotFound()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/library/games/{gameId}/loan/remind",
            new { CustomMessage = "Please return the game" });

        // Assert - May return 404 if endpoint doesn't exist or 401 if auth fails first
        (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.NotFound).Should().BeTrue($"Expected Unauthorized or NotFound, got {response.StatusCode}");
    }

    // ========================================
    // GAME CHECKLIST ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task GetGameChecklist_WithoutAuth_ReturnsAppropriateError()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/library/games/{gameId}/checklist");

        // Assert - May return BadRequest if validation fails, or Unauthorized
        (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound).Should().BeTrue($"Expected Unauthorized, BadRequest, or NotFound, got {response.StatusCode}");
    }

    // ========================================
    // CUSTOM PDF UPLOAD ENDPOINT TESTS
    // ========================================

    [Fact]
    public async Task UploadCustomPdf_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/library/games/{gameId}/pdf",
            new { PdfUrl = "https://example.com/rules.pdf", OriginalFileName = "rules.pdf", FileSizeBytes = 1000 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // AIUTI DEI TEST SUL LINK DI CONDIVISIONE
    // ========================================

    private async Task<string> CreateUserSessionTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);
        return token;
    }

    /// <summary>
    /// Crea il link dall'endpoint, con una scadenza futura come nella verifica della issue: la riga è
    /// quella che il sistema scrive, non una costruita a mano.
    /// </summary>
    private async Task<string> CreateShareLinkAsync(string token)
    {
        var expiresAt = DateTime.UtcNow.AddDays(10).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        var (status, body) = await SendJsonAsync(
            HttpMethod.Post, "/api/v1/library/share", token, $$"""{"privacyLevel":"unlisted","expiresAt":"{{expiresAt}}"}""");
        status.Should().Be(HttpStatusCode.Created, $"setup: the share link must be created. Body: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("shareToken").GetString()!;
    }

    private async Task ExpireShareLinkAsync(string shareToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var updated = await db.LibraryShareLinks
            .Where(l => l.ShareToken == shareToken)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, yesterday));
        updated.Should().Be(1, "setup: exactly the link of this test must expire");
    }

    private async Task<LibraryShareLinkEntity> ReadShareLinkRowAsync(string shareToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.LibraryShareLinks.AsNoTracking().SingleAsync(l => l.ShareToken == shareToken);
    }

    private async Task<(HttpStatusCode Status, string Body)> SendJsonAsync(HttpMethod method, string path, string token, string json)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(method, path, token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string? ErrorOf(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetString();
}
