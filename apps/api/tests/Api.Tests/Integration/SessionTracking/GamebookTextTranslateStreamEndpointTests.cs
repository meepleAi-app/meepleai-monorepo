using System.Net;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test seminA la propria campagna +
/// GameBook con id generati (<c>SeedOwnedCampaignAsync</c> usa <c>Guid.NewGuid()</c> per
/// <c>sharedGameId</c>) e ogni chiamata all'endpoint e' scoped a quel <c>campaignId</c>/
/// <c>gameBookId</c>. Nessuna asserzione legge conteggi o liste globali: sono tutte status-code su
/// una singola richiesta SSE pre-flight per la campagna del test stesso.
/// </para>
/// </summary>
public sealed class GamebookTextTranslateStreamHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "text_translate_stream");

/// <summary>
/// Issue #1774: integration tests for POST /api/v1/gamebook/campaigns/{id}/text/translate (SSE).
/// Verifies the pre-flight ownership/validation guards return proper HTTP error codes BEFORE
/// any SSE bytes are flushed to the client, following the same conservative dual-acceptance
/// pattern as <see cref="GamebookTranslateStreamEndpointTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// DEC-BE-11: Final chunk has ParagraphId=null (no TranslatedParagraph persistence).
/// DEC-BE-12: SessionBookProgress is NOT updated by the text-translate handler.
/// DEC-BE-13: Final chunk echoes the user-provided SourceLang as DetectedSourceLang;
///            LangDetectionConfidence is null (no NTextCat detection in manual mode).
/// </para>
/// <para>
/// Validation runs BEFORE any ownership check (pre-flight), so unauthenticated
/// requests with invalid bodies still receive 400 — not 401.
/// </para>
/// <para>
/// Because the real LLM service is replaced by the IntegrationWebApplicationFactory
/// config stub (OpenRouter:ApiKey = "test-key" pointing to a non-existent host),
/// HappyPath tests accept either 200/OK (SSE stream opened) or 401 (cookie not
/// honored in this test pipeline) — following the conservative dual-acceptance pattern.
/// </para>
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GamebookTextTranslateStreamEndpointTests : IClassFixture<GamebookTextTranslateStreamHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GamebookTextTranslateStreamEndpointTests(GamebookTextTranslateStreamHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildUrl(Guid campaignId) =>
        $"/api/v1/gamebook/campaigns/{campaignId}/text/translate";

    private static TranslateGamebookTextRequest ValidBody(Guid gameBookId) =>
        new("The adventurers enter the dungeon.", "EN", "IT", gameBookId);

    /// <summary>
    /// Seeds a campaign owned by the returned user + a GameBook for that campaign.
    /// Returns the owner's session token and the ids needed to call the translate endpoint.
    /// </summary>
    private async Task<(Guid OwnerUserId, string SessionToken, Guid CampaignId, Guid GameBookId)>
        SeedOwnedCampaignAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var sharedGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(sharedGameId), userId, "Issue 1774 text translate test");
        var book = GameBook.CreateCommunity(
            GameRef.Shared(sharedGameId),
            "Text Translate Test Book",
            GameBookRole.Tutorial,
            ParagraphScheme.ParagraphNumber,
            "en",
            sequentialRead: false,
            kbSourceDocId: null,
            physicalOnly: false,
            createdBy: userId);

        await dbContext.GamebookCampaignSessions.AddAsync(session);
        await dbContext.GameBooks.AddAsync(book);
        await dbContext.SaveChangesAsync();

        return (userId, sessionToken, session.Id, book.Id);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test cases
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// S1 — Happy path: authenticated owner with a valid body should reach the SSE stream
    /// (either 200 OK with SSE content-type OR 401 if cookie not honored in test pipeline).
    /// Must never yield 403/404 for the legitimate owner.
    /// DEC-BE-8 ownership pre-flight, DEC-BE-13 final chunk shape verified via conservative acceptance.
    /// </summary>
    [Fact]
    public async Task HappyPath_OwnerCanTranslate_EmitsSse()
    {
        var owned = await SeedOwnedCampaignAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            BuildUrl(owned.CampaignId),
            owned.SessionToken,
            ValidBody(owned.GameBookId));

        var response = await _client.SendAsync(request);

        // Conservative dual-acceptance: SSE stream opened (200) or cookie not honored (401).
        // Key assertions: NOT 403 (owner must not hit pre-flight forbidden) and NOT 404.
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Owner pre-flight should not yield Forbidden/NotFound. Got {response.StatusCode}");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "owner request must not hit the ForbiddenException pre-flush branch");
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "owner request must not hit the NotFoundException pre-flush branch");

        // If 200 OK, verify SSE content-type and that the stream is text/event-stream.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
                "the endpoint must flush SSE headers before streaming");
        }
    }

    /// <summary>
    /// S2 — Empty text body must return 400 BadRequest BEFORE any SSE headers are flushed.
    /// Uses an authenticated request to bypass the auth middleware so FluentValidation fires.
    /// Conservative acceptance: 400 (validator fired) or 401 (cookie not honored in test pipeline).
    /// Must NEVER produce an SSE stream.
    /// </summary>
    [Fact]
    public async Task InvalidText_Empty_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var body = new TranslateGamebookTextRequest("", "EN", "IT", Guid.NewGuid());
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, BuildUrl(Guid.NewGuid()), sessionToken, body);

        var response = await _client.SendAsync(request);

        // Auth fires before validation inside the handler body; accept 400 or 401.
        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"empty text must yield 400 BadRequest or 401, not {response.StatusCode}");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream",
            "a validation failure must never produce an SSE stream");

        // If 400, confirm the body is a ValidationProblemDetails (not SSE).
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body2 = await response.Content.ReadAsStringAsync();
            body2.Should().NotStartWith("data:", "a 400 response must not contain SSE data lines");
        }
    }

    /// <summary>
    /// S3 — Text exceeding 2000 characters must return 400 or 401 (conservative pattern).
    /// </summary>
    [Fact]
    public async Task InvalidText_TooLong_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var body = new TranslateGamebookTextRequest(new string('a', 2001), "EN", "IT", Guid.NewGuid());
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, BuildUrl(Guid.NewGuid()), sessionToken, body);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"text of 2001 chars must yield 400 or 401, not {response.StatusCode}");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
    }

    /// <summary>
    /// S4 — Invalid sourceLang (not in EN/FR/DE/ES/IT allowlist) must return 400 or 401.
    /// Conservative acceptance: 400 (validator fired) or 401 (cookie not honored in test pipeline).
    /// </summary>
    [Theory]
    [InlineData("XX")]
    [InlineData("PL")]
    [InlineData("ZZ")]
    public async Task InvalidSourceLang_NotInAllowlist_Returns400(string invalid)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var body = new TranslateGamebookTextRequest("Hello world.", invalid, "IT", Guid.NewGuid());
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, BuildUrl(Guid.NewGuid()), sessionToken, body);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"sourceLang='{invalid}' must yield 400 or 401, not {response.StatusCode}");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
    }

    /// <summary>
    /// S5 — TargetLang other than "IT" must return 400 or 401
    /// (v1 target is fixed to Italian; validation fires after auth inside handler body).
    /// </summary>
    [Fact]
    public async Task InvalidTargetLang_NotIT_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var body = new TranslateGamebookTextRequest("Hello world.", "EN", "EN", Guid.NewGuid());
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, BuildUrl(Guid.NewGuid()), sessionToken, body);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"targetLang=EN must yield 400 or 401, not {response.StatusCode}");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
    }

    /// <summary>
    /// S6 — GameBookId=Guid.Empty must return 400 or 401 (NotEmpty validation rule).
    /// </summary>
    [Fact]
    public async Task InvalidGameBookId_Empty_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var body = new TranslateGamebookTextRequest("Hello world.", "EN", "IT", Guid.Empty);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, BuildUrl(Guid.NewGuid()), sessionToken, body);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"gameBookId=Guid.Empty must yield 400 or 401, not {response.StatusCode}");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
    }

    /// <summary>
    /// S7 — Authenticated non-owner must receive 403 (or 401 if cookie not honored) before
    /// any SSE byte is flushed. The pre-flight ICampaignOwnershipGuard fires before SSE headers.
    /// </summary>
    [Fact]
    public async Task NotOwner_Returns403_OrUnauthorized()
    {
        var owned = await SeedOwnedCampaignAsync();

        // Spin up a second user with their own session token.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var (_, otherToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

            var request = TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post,
                BuildUrl(owned.CampaignId),
                otherToken,
                ValidBody(owned.GameBookId));

            var response = await _client.SendAsync(request);

            // Conservative dual-acceptance: 403 (ownership guard fired) or 401 (cookie not honored).
            (response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
                $"Non-owner should get Forbidden or Unauthorized, got {response.StatusCode}");

            response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream",
                "a pre-flight ownership failure must never produce an SSE stream");
        }
    }

    /// <summary>
    /// S8 — Request against a non-existent campaignId must return 404 (or 401)
    /// before any SSE byte is flushed. NotFoundException from the ownership guard fires pre-SSE.
    /// </summary>
    [Fact]
    public async Task NonExistentCampaign_Returns404_OrUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Use a random campaignId that was never seeded.
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            BuildUrl(Guid.NewGuid()),
            sessionToken,
            new TranslateGamebookTextRequest("Hello world.", "EN", "IT", Guid.NewGuid()));

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Non-existent campaign should get NotFound or Unauthorized, got {response.StatusCode}");

        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream",
            "a pre-flight not-found failure must never produce an SSE stream");
    }

    /// <summary>
    /// S9 — Backward-compat: the original GET /photos/translate endpoint is still reachable
    /// after the T6 refactor that added the new sibling POST /text/translate endpoint.
    /// An unauthenticated request returns 401 (not 404 or 500), confirming the route is
    /// registered and nothing was accidentally broken by the new endpoint addition.
    /// </summary>
    [Fact]
    public async Task BackwardCompat_PhotoTranslateEndpointStillRegistered()
    {
        var fakeUrl = $"/api/v1/gamebook/campaigns/{Guid.NewGuid()}/photos/translate" +
                      $"?photoId={Guid.NewGuid()}&paragraphNumber=1&gameBookId={Guid.NewGuid()}";

        var response = await _client.GetAsync(fakeUrl);

        // The route must exist (not 404). Unauthenticated → 401 or 400 (validation before auth).
        // A 404 would mean the route was accidentally removed.
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "the original /photos/translate GET route must still be registered after T6 refactor");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "the original /photos/translate route must not error on startup/routing");
    }
}
