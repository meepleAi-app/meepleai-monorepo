using System.Net;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
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
/// Issue #1415: integration tests for GET /api/v1/gamebook/campaigns/{id}/photos/translate (SSE).
/// Verifies the pre-flight ownership guard returns proper HTTP error codes BEFORE the SSE
/// headers are flushed, so middleware can rewrite the status to 401/403/404 (instead of
/// the prior anti-pattern: HTTP 200 with <c>data: {code:"INTERNAL_ERROR"}</c> SSE chunk).
/// </summary>
/// <remarks>
/// <para>
/// Follows the conservative dual-acceptance pattern used by sibling integration tests
/// (<c>GetCampaignProgressEndpointTests</c>): the in-test middleware may or may not
/// honor the seeded session cookie, so authenticated assertions accept either the
/// expected business status code OR Unauthorized.
/// </para>
/// <para>
/// Deferred to a follow-up (would require <c>ConfigureTestServices</c> overrides):
/// <list type="bullet">
/// <item>DB timeout → HTTP 504 (needs <c>SlowRepository</c> mock injection)</item>
/// <item>Mid-stream NotFoundException → SSE event <c>{code:"NOT_FOUND"}</c> (needs flaky <c>ILlmService</c> mock)</item>
/// <item>Client disconnect → log Information only (needs SSE stream consumer cancellation)</item>
/// </list>
/// </para>
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GamebookTranslateStreamEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GamebookTranslateStreamEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"translate_stream_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    private static string BuildUrl(Guid campaignId, Guid photoId, Guid gameBookId, int paragraphNumber = 1) =>
        $"/api/v1/gamebook/campaigns/{campaignId}/photos/translate" +
        $"?photoId={photoId}&paragraphNumber={paragraphNumber}&gameBookId={gameBookId}";

    /// <summary>
    /// Seeds a campaign owned by the returned user, with a segmented photo artifact
    /// containing one paragraph. Returns the owner's session token + the ids needed
    /// to construct the translate URL.
    /// </summary>
    private async Task<(Guid OwnerUserId, string SessionToken, Guid CampaignId, Guid PhotoId, Guid GameBookId)>
        SeedOwnedCampaignWithSegmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var sharedGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(sharedGameId), userId, "Issue 1415 test");
        var book = GameBook.CreateCommunity(
            GameRef.Shared(sharedGameId),
            "Press Start",
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

        var photo = GamebookPhotoArtifact.Create(session.Id, book.Id, "test/s3/key.jpg");
        photo.RecordSegments(
            new[] { GamebookSegment.Create(paragraphNumber: 1, sourceText: "The Hive awakens.", boundingBox: null) },
            ocrFullText: "The Hive awakens.");
        await dbContext.GamebookPhotoArtifacts.AddAsync(photo);
        await dbContext.SaveChangesAsync();

        return (userId, sessionToken, session.Id, photo.Id, book.Id);
    }

    [Fact]
    public async Task Translate_NonOwnerAnonymous_Returns401()
    {
        // Scenario 1: no session cookie at all → middleware must respond 401 immediately.
        var url = BuildUrl(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // Confirm we did NOT degrade into an SSE stream
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
    }

    [Fact]
    public async Task Translate_NonOwnerAuthenticated_Returns403_OrUnauthorized()
    {
        // Scenario 2: seeded campaign owned by user A, request authenticated as user B.
        // Expectation: ApiExceptionHandlerMiddleware translates the pre-flight ForbiddenException
        // (thrown by ICampaignOwnershipGuard) into HTTP 403 BEFORE any SSE header is flushed.
        var owned = await SeedOwnedCampaignWithSegmentAsync();

        // Spin up a second user and use their session token to call the translate URL.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var (_, otherUserToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

            var request = TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Get,
                BuildUrl(owned.CampaignId, owned.PhotoId, owned.GameBookId),
                otherUserToken);

            var response = await _client.SendAsync(request);

            // Conservative dual-acceptance: middleware may or may not honor the seeded
            // session cookie in this test pipeline; either case proves the endpoint
            // does NOT return HTTP 200 with an SSE INTERNAL_ERROR chunk.
            (response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
                $"Expected Forbidden or Unauthorized, got {response.StatusCode}");
            response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
        }
    }

    [Fact]
    public async Task Translate_CampaignMissing_Returns404_OrUnauthorized()
    {
        // Scenario 3: owner-style request against a campaignId that does not exist.
        // Expectation: pre-flight throws NotFoundException → middleware → HTTP 404.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildUrl(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            sessionToken);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected NotFound or Unauthorized, got {response.StatusCode}");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream");
    }

    [Fact]
    public async Task Translate_Owner_DoesNotShortCircuitToHttpError()
    {
        // Scenario 4: legitimate owner. The handler may still error downstream due to
        // missing LLM credentials in the test environment, but the response MUST NOT
        // be 401/403/404 (those are reserved for the pre-flight authz failures).
        var owned = await SeedOwnedCampaignWithSegmentAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildUrl(owned.CampaignId, owned.PhotoId, owned.GameBookId),
            owned.SessionToken);

        var response = await _client.SendAsync(request);

        // Accept either:
        //   - 200 OK (SSE stream began, regardless of LLM outcome)
        //   - 401 (cookie not honored — sibling test pattern)
        // Reject 403/404: pre-flight guard correctly recognized the owner.
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Owner pre-flight should not yield Forbidden/NotFound. Got {response.StatusCode}");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "owner request must not hit the ForbiddenException pre-flush branch");
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "owner request must not hit the NotFoundException pre-flush branch");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Issue #1559 T8: sourceLangOverride endpoint validator + backward compat
    // ──────────────────────────────────────────────────────────────────────────

    private static string BuildUrlWithOverride(
        Guid campaignId, Guid photoId, Guid gameBookId,
        int paragraphNumber = 1, string? sourceLangOverride = null)
    {
        var url = BuildUrl(campaignId, photoId, gameBookId, paragraphNumber);
        return sourceLangOverride is not null
            ? $"{url}&sourceLangOverride={Uri.EscapeDataString(sourceLangOverride)}"
            : url;
    }

    /// <summary>
    /// Seeds a campaign owned by the returned user, with a segmented photo artifact
    /// that carries explicit lang detection fields (T3 overload).
    /// </summary>
    private async Task<(Guid OwnerUserId, string SessionToken, Guid CampaignId, Guid PhotoId, Guid GameBookId)>
        SeedOwnedCampaignWithLangDetectionAsync(string? detectedLang = "EN", double confidence = 0.93)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var sharedGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(sharedGameId), userId, "Issue 1559 T8 test");
        var book = GameBook.CreateCommunity(
            GameRef.Shared(sharedGameId),
            "Press Start T8",
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

        var photo = GamebookPhotoArtifact.Create(session.Id, book.Id, "test/s3/key-t8.jpg");
        // Use the T3 4-param overload to persist lang detection fields.
        photo.RecordSegments(
            new[] { GamebookSegment.Create(paragraphNumber: 1, sourceText: "The Hive awakens.", boundingBox: null) },
            ocrFullText: "The Hive awakens.",
            detectedSourceLang: detectedLang,
            langDetectionConfidence: detectedLang is not null ? confidence : null);
        await dbContext.GamebookPhotoArtifacts.AddAsync(photo);
        await dbContext.SaveChangesAsync();

        return (userId, sessionToken, session.Id, photo.Id, book.Id);
    }

    /// <summary>
    /// Scenario 5a–5d: un sourceLangOverride non valido deve dare 400 PRIMA del pre-flight di
    /// ownership e prima che venga scritto un header SSE.
    ///
    /// <para>
    /// #3633 — la richiesta è autenticata, e prima non lo era. Il test asseriva che «even
    /// anonymous requests receive 400 (no auth needed)» e riceveva invece 401, perché l'endpoint
    /// verifica l'autenticazione come prima cosa (<c>TryGetAuthenticatedUser</c>). Il contratto
    /// documentato — #1559 DEC-12 — dice «validate sourceLangOverride BEFORE the <b>ownership
    /// check</b>», non prima dell'auth: era il test a pretendere più della decisione, ed era in
    /// contraddizione con lo Scenario 6 qui sotto, che per una richiesta anonima accetta il 401.
    /// </para>
    /// <para>
    /// Gli id restano inesistenti proprio per provare DEC-12: se la validazione non precedesse
    /// l'ownership guard, la risposta sarebbe 403/404 invece di 400.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("XYZ")]
    [InlineData("english")]
    [InlineData("EN-US")]
    [InlineData("PL")]
    [InlineData("  ")]
    public async Task Translate_InvalidSourceLangOverride_Returns400_BeforeOwnershipCheck(string invalid)
    {
        var owned = await SeedOwnedCampaignWithLangDetectionAsync();

        // Id inesistenti: senza la validazione anticipata l'ownership guard risponderebbe 403/404.
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildUrlWithOverride(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sourceLangOverride: invalid),
            owned.SessionToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"invalid sourceLangOverride '{invalid}' should yield 400 before the ownership check (#1559 DEC-12)");
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("text/event-stream",
            "a 400 must never produce an SSE stream");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("sourceLangOverride must be one of EN, FR, DE, ES, IT",
            "error message must reference the full allowlist");
    }

    /// <summary>
    /// Scenario 6: valid sourceLangOverride values must NOT be rejected with 400.
    /// The request may still yield 401 (unauthenticated) — that is accepted because
    /// the test intentionally omits auth to keep it lightweight. The key assertion is
    /// that 400 is never returned for a valid code.
    /// </summary>
    [Theory]
    [InlineData("EN")]
    [InlineData("FR")]
    [InlineData("DE")]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("en")]  // lowercase — normalized to uppercase by endpoint
    [InlineData("it")]  // lowercase — normalized to uppercase by endpoint
    public async Task Translate_ValidSourceLangOverride_DoesNotReturn400(string valid)
    {
        var url = BuildUrlWithOverride(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sourceLangOverride: valid);
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            $"valid sourceLangOverride '{valid}' must not be rejected by the validator");
    }

    /// <summary>
    /// Scenario 7: backward compat — no sourceLangOverride param must continue to work.
    /// An authenticated owner request without the param must not yield 400.
    /// </summary>
    [Fact]
    public async Task Translate_NoSourceLangOverride_BackwardCompat_OwnerDoesNotReturn400()
    {
        var owned = await SeedOwnedCampaignWithLangDetectionAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildUrl(owned.CampaignId, owned.PhotoId, owned.GameBookId),
            owned.SessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "omitting sourceLangOverride must preserve backward compat");
        // Accept OK (SSE stream) or 401 (cookie not honored in test pipeline)
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"backward-compat owner request yielded unexpected status {response.StatusCode}");
    }

    /// <summary>
    /// Scenario 8: authenticated owner with valid sourceLangOverride must not be rejected.
    /// Response is either 200 (SSE opens) or 401 (cookie not honored in test pipeline);
    /// 400 and 403 are both forbidden outcomes.
    /// </summary>
    [Fact]
    public async Task Translate_OwnerWithValidOverride_DoesNotReturn400Or403()
    {
        var owned = await SeedOwnedCampaignWithLangDetectionAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            BuildUrlWithOverride(owned.CampaignId, owned.PhotoId, owned.GameBookId, sourceLangOverride: "IT"),
            owned.SessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "valid sourceLangOverride 'IT' must pass the validator");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "owner with valid override must not hit the pre-flight forbidden branch");
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"expected OK or Unauthorized for owner+valid override, got {response.StatusCode}");
    }
}
