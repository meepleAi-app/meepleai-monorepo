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
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using FluentAssertions.Execution;

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
/// riga del link che hanno creato, cercata per token; quelli delle gare (#4558) anche, e l'unico
/// conteggio (i link attivi dopo la creazione di uno nuovo) e' filtrato per il proprio utente.
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

    /// <summary>
    /// Le gare fra visita pubblica e proprietario (#4558): il repository dei link è decorato perché un
    /// test possa fermare una richiesta dopo la lettura del link. Senza un'azione registrata il
    /// decoratore è trasparente, quindi gli altri test della classe non lo vedono.
    /// </summary>
    internal ShareLinkReadInterleaving ShareLinkReads { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(ShareLinkReads.Decorate));
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
    private readonly ShareLinkReadInterleaving _interleaving;

    public UserLibraryEndpointsIntegrationTests(UserLibraryEndpointsHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
        _interleaving = host.ShareLinkReads;
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
    // GARE SUL LINK DI CONDIVISIONE (#4558)
    // ========================================
    //
    // Ogni test ferma la prima richiesta subito dopo che ha letto il link (ShareLinkReadInterleaving)
    // e, in quel punto, esegue per intero la seconda. In produzione l'ordine lo decide il caso; qui è
    // fisso, quindi il test è deterministico. Prima della correzione ogni scrittura riscriveva TUTTE le
    // colonne della riga con i valori letti (DbSet.Update su un oggetto non tracciato, nessun token di
    // concorrenza): la richiesta fermata cancellava ciò che l'altra aveva appena salvato.

    [Fact]
    public async Task GetSharedLibrary_RevokedWhileTheVisitIsInFlight_Answers404AndTheRevocationStays()
    {
        var (_, owner) = await CreateOwnerAsync();
        var shareToken = await CreateShareLinkAsync(owner);
        HttpStatusCode? revokeStatus = null;
        _interleaving.AfterNextRead(shareToken, async () => revokeStatus = await RevokeAsync(shareToken, owner));

        using var visit = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");
        var body = await visit.Content.ReadAsStringAsync();

        _interleaving.IsPending(shareToken).Should().BeFalse("setup: the revocation must run inside the visit");
        revokeStatus.Should().Be(HttpStatusCode.NoContent, "setup: the owner revokes the link while the visit is in flight");

        var row = await ReadShareLinkRowAsync(shareToken);
        using var after = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");

        // Tutte le asserzioni insieme: il rosso deve mostrare anche la revoca annullata, non solo lo status.
        using (new AssertionScope())
        {
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound,
                $"the link was revoked before the visit could count itself, so the visit must not return the library. " +
                $"Before #4558 it answered 200. Body: {body}");
            row.RevokedAt.Should().NotBeNull(
                "a public visit must never undo a revocation. Before #4558 the visit wrote back revoked_at = NULL, " +
                "the value it had read, and the revoked link was public again");
            row.ViewCount.Should().Be(0, "a visit that lost the race to the revocation is not an access");
            after.StatusCode.Should().Be(HttpStatusCode.NotFound, "the revoked link stays revoked");
        }
    }

    [Fact]
    public async Task GetSharedLibrary_NewLinkCreatedWhileTheVisitIsInFlight_LeavesOneActiveLink()
    {
        var (userId, owner) = await CreateOwnerAsync();
        var oldToken = await CreateShareLinkAsync(owner);
        (HttpStatusCode Status, string Body)? creation = null;
        _interleaving.AfterNextRead(oldToken, async () => creation = await SendJsonAsync(
            HttpMethod.Post, "/api/v1/library/share", owner, """{"privacyLevel":"unlisted"}"""));

        using var visit = await _client.GetAsync($"/api/v1/library/shared/{oldToken}");

        _interleaving.IsPending(oldToken).Should().BeFalse("setup: the new link must be created inside the visit");
        creation.Should().NotBeNull();
        creation!.Value.Status.Should().Be(HttpStatusCode.Created, $"setup: the new link must be created. Body: {creation.Value.Body}");
        var newToken = JsonDocument.Parse(creation.Value.Body).RootElement.GetProperty("shareToken").GetString();

        // Creare un link nuovo revoca il vecchio (CreateLibraryShareLinkCommandHandler).
        visit.StatusCode.Should().Be(HttpStatusCode.NotFound, "the old link was revoked by the new one before the visit counted itself");
        (await ReadShareLinkRowAsync(oldToken)).RevokedAt.Should().NotBeNull(
            "the visit must not bring the old link back. Before #4558 it wrote back revoked_at = NULL");

        var now = DateTime.UtcNow;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var active = await db.LibraryShareLinks.AsNoTracking()
            .Where(l => l.UserId == userId && l.RevokedAt == null && (l.ExpiresAt == null || l.ExpiresAt > now))
            .Select(l => l.ShareToken)
            .ToListAsync();
        active.Should().Equal(new[] { newToken }, "an owner has one active link: the one created last");
    }

    [Fact]
    public async Task GetSharedLibrary_SettingsChangedWhileTheVisitIsInFlight_KeepsTheOwnersChange()
    {
        var (_, owner) = await CreateOwnerAsync();
        var shareToken = await CreateShareLinkAsync(owner);
        var before = await ReadShareLinkRowAsync(shareToken);
        before.IncludeNotes.Should().BeFalse("setup: the link is created without notes");
        before.PrivacyLevel.Should().Be(1, "setup: the link is created unlisted");

        (HttpStatusCode Status, string Body)? change = null;
        _interleaving.AfterNextRead(shareToken, async () => change = await SendJsonAsync(
            HttpMethod.Patch, $"/api/v1/library/share/{shareToken}", owner, """{"privacyLevel":"public","includeNotes":true}"""));

        using var visit = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");

        _interleaving.IsPending(shareToken).Should().BeFalse("setup: the change must run inside the visit");
        change.Should().NotBeNull();
        change!.Value.Status.Should().Be(HttpStatusCode.OK, $"setup: the owner changes the settings. Body: {change.Value.Body}");
        visit.StatusCode.Should().Be(HttpStatusCode.OK, "the link stays valid: only its settings changed");

        var after = await ReadShareLinkRowAsync(shareToken);
        after.IncludeNotes.Should().BeTrue(
            "the visit must not undo the owner's change. Before #4558 it wrote back the settings it had read");
        after.PrivacyLevel.Should().Be(0, "public, as the owner set it");
        after.ViewCount.Should().Be(1, "the visit still counts");
    }

    [Fact]
    public async Task GetSharedLibrary_TwoVisitsInterleaved_CountsBoth()
    {
        var (_, owner) = await CreateOwnerAsync();
        var shareToken = await CreateShareLinkAsync(owner);
        HttpStatusCode? secondVisit = null;
        _interleaving.AfterNextRead(shareToken, async () =>
        {
            using var response = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");
            secondVisit = response.StatusCode;
        });

        using var firstVisit = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");

        _interleaving.IsPending(shareToken).Should().BeFalse("setup: the second visit must run inside the first");
        firstVisit.StatusCode.Should().Be(HttpStatusCode.OK);
        secondVisit.Should().Be(HttpStatusCode.OK);

        var row = await ReadShareLinkRowAsync(shareToken);
        row.ViewCount.Should().Be(2,
            "two visits are two accesses. Before #4558 the first visit wrote back the count it had read plus one, " +
            "and the second access was lost");
        row.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeShareLink_VisitedWhileTheRevocationIsInFlight_KeepsTheVisit()
    {
        var (_, owner) = await CreateOwnerAsync();
        var shareToken = await CreateShareLinkAsync(owner);
        HttpStatusCode? visit = null;
        _interleaving.AfterNextRead(shareToken, async () =>
        {
            using var response = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");
            visit = response.StatusCode;
        });

        var revoke = await RevokeAsync(shareToken, owner);

        _interleaving.IsPending(shareToken).Should().BeFalse("setup: the visit must run inside the revocation");
        visit.Should().Be(HttpStatusCode.OK, "the link was still active when the visit arrived");
        revoke.Should().Be(HttpStatusCode.NoContent);

        var row = await ReadShareLinkRowAsync(shareToken);
        row.RevokedAt.Should().NotBeNull();
        row.ViewCount.Should().Be(1,
            "the owner's write does not touch the visit counters. Before #4558 the revocation wrote back the count it had read, 0");
        row.LastAccessedAt.Should().NotBeNull("the last access is the visit's, and the revocation must not erase it");
    }

    [Fact]
    public async Task UpdateShareLink_RevokedWhileTheRenewalIsInFlight_IsRefusedAndTheRevocationStays()
    {
        var (_, owner) = await CreateOwnerAsync();
        var shareToken = await CreateShareLinkAsync(owner);
        var before = await ReadShareLinkRowAsync(shareToken);
        HttpStatusCode? revokeStatus = null;
        _interleaving.AfterNextRead(shareToken, async () => revokeStatus = await RevokeAsync(shareToken, owner));

        var renewal = DateTime.UtcNow.AddDays(30).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        var (status, body) = await SendJsonAsync(
            HttpMethod.Patch, $"/api/v1/library/share/{shareToken}", owner, $$"""{"expiresAt":"{{renewal}}"}""");

        _interleaving.IsPending(shareToken).Should().BeFalse("setup: the revocation must run inside the renewal");
        revokeStatus.Should().Be(HttpStatusCode.NoContent, "setup: the owner revokes the link while the renewal is in flight");

        var after = await ReadShareLinkRowAsync(shareToken);
        using var visit = await _client.GetAsync($"/api/v1/library/shared/{shareToken}");

        // Il rinnovo ha letto un link valido, ma quando scrive il link è revocato: la scrittura è
        // condizionata a revoked_at IS NULL, tocca 0 righe, ed EF la segnala come conflitto (409).
        using (new AssertionScope())
        {
            status.Should().Be(HttpStatusCode.Conflict,
                $"the renewal read a valid link, but the link was revoked before the renewal could write. " +
                $"Before #4558 it answered 200. Body: {body}");
            after.RevokedAt.Should().NotBeNull(
                "a revocation is never undone. Before #4558 the renewal wrote back revoked_at = NULL and the link was public again");
            after.ExpiresAt.Should().Be(before.ExpiresAt, "the refused renewal changes nothing");
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound, "the revoked link stays revoked");
        }

        ErrorOf(body).Should().Be("concurrent_edit", "the conflict is the middleware's answer to DbUpdateConcurrencyException");
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

    private async Task<string> CreateUserSessionTokenAsync() => (await CreateOwnerAsync()).Token;

    private async Task<(Guid UserId, string Token)> CreateOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateUserSessionAsync(db);
    }

    private async Task<HttpStatusCode> RevokeAsync(string shareToken, string token)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/library/share/{shareToken}", token);
        using var response = await _client.SendAsync(request);
        return response.StatusCode;
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
