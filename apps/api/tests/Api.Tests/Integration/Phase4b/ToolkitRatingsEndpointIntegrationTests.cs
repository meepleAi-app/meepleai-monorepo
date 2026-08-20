using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Phase4b;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> <c>GET /toolkits/{id}/ratings</c> e'
/// uno stub (Gate B v1 — l'entita' <c>ToolkitRating</c> non esiste ancora): restituisce sempre
/// <c>{ items: [], totalCount: 0, averageStars: 0 }</c> indipendentemente da quanti toolkit o
/// rating siano stati seminati da altri test, quindi non c'e' alcun conteggio o ordinamento globale
/// da cui i test possano interferire. Le uniche asserzioni non-stub riguardano lo status code per un
/// singolo <c>toolkitId</c> creato dal test stesso.
/// </para>
/// </summary>
public sealed class ToolkitRatingsEndpointHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "toolkit_ratings");

/// <summary>
/// Integration tests for the toolkit ratings surface
/// (Wave 3 Phase 4b, PR #732 §5.3.3 + §5.3.4 / Issue #805).
///
/// <para>
/// Schema reality v1 carryover (Gate B): the <c>ToolkitRating</c> entity does
/// not exist yet. <c>GET /toolkits/{id}/ratings</c> returns an empty stub
/// envelope once the visibility check passes; <c>POST /toolkits/{id}/ratings</c>
/// returns 501 Not Implemented with a machine-readable body. Wire shape is
/// stable so the FE can render today and adopt real persistence in Phase 5.
/// </para>
///
/// <para>
/// Visibility rule (PR #732 §5.2 security boundary): non-authors must not
/// learn about drafts/yanked toolkits via the ratings surface — the handler
/// returns 404 in that case, mirroring the toolkit detail endpoint.
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameToolkit")]
[Trait("Wave", "3-Phase-4b")]
public sealed class ToolkitRatingsEndpointIntegrationTests : IClassFixture<ToolkitRatingsEndpointHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ToolkitRatingsEndpointIntegrationTests(ToolkitRatingsEndpointHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // ───────────────────────── GET /toolkits/{id}/ratings ─────────────────────

    [Fact]
    public async Task GetRatings_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1/toolkits/{Guid.NewGuid()}/ratings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRatings_ForUnknownToolkit_Returns404()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{Guid.NewGuid()}/ratings",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRatings_OnDraftToolkit_NonOwner_Returns404()
    {
        // PR #732 §5.2 security boundary — non-authors must not learn that a
        // draft toolkit exists via the ratings surface.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var toolkitId = await SeedToolkitAsync(dbContext, authorId, "Hidden Draft",
            isPublished: false, templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkitId}/ratings",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRatings_OnDraftToolkit_AsOwner_Returns200WithEmptyStub()
    {
        // Authors must always see their own toolkits, even drafts.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, authorToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var toolkitId = await SeedToolkitAsync(dbContext, authorId, "My Draft",
            isPublished: false, templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkitId}/ratings",
            authorToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertEmptyStubShape(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task GetRatings_OnPublishedToolkit_Returns200WithEmptyStub()
    {
        // PR #732 §3.4 empty-state contract: 200 with { items: [] }, breakdown
        // all zero, averageStars=0, totalCount=0 (Gate B v1 — no entity yet).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var toolkitId = await SeedToolkitAsync(dbContext, authorId, "Public Toolkit",
            isPublished: true, templateStatus: TemplateStatus.Approved);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkitId}/ratings?limit=20",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertEmptyStubShape(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task GetRatings_WithCursor_StillReturnsEmptyStub()
    {
        // Cursor is opaque in v1 — handler always returns nextCursor=null
        // until persistence ships in Phase 5.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var toolkitId = await SeedToolkitAsync(dbContext, authorId, "Public",
            isPublished: true, templateStatus: TemplateStatus.Approved);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkitId}/ratings?cursor=arbitrary-token&limit=10",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        AssertEmptyStubShape(body);
    }

    [Fact]
    public async Task GetRatings_LimitClamping_AcceptsOutOfRangeValues()
    {
        // Endpoint silent-clamps limit (PR #732 §3.3 UI-friendly behavior).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var toolkitId = await SeedToolkitAsync(dbContext, authorId, "Public",
            isPublished: true, templateStatus: TemplateStatus.Approved);

        // limit=0 → clamped to default 20, limit=999 → clamped to 50
        foreach (var url in new[]
                 {
                     $"/api/v1/toolkits/{toolkitId}/ratings?limit=0",
                     $"/api/v1/toolkits/{toolkitId}/ratings?limit=999",
                 })
        {
            var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, url, viewerToken);
            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"limit clamp for {url}");
        }
    }

    // ───────────────────────── POST /toolkits/{id}/ratings (stub) ─────────────

    [Fact]
    public async Task SubmitRating_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/toolkits/{Guid.NewGuid()}/ratings",
            new { stars = 5, comment = "Great!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SubmitRating_Authenticated_Returns501WithStubBody()
    {
        // Phase 4b stub — wire shape reserved, FE detects via 501 status +
        // machine-readable body.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkitId = Guid.NewGuid();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{toolkitId}/ratings",
            sessionToken);
        request.Content = JsonContent.Create(new { stars = 5, comment = "Great!" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("ratings_submission_not_yet_available");
        body.GetProperty("phase").GetString().Should().Be("4b-stub");
        body.GetProperty("toolkitId").GetGuid().Should().Be(toolkitId);
    }

    // ───────────────────────── helpers ────────────────────────────────────────

    private static void AssertEmptyStubShape(JsonElement body)
    {
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
        body.GetProperty("averageStars").GetDecimal().Should().Be(0m);

        var breakdown = body.GetProperty("breakdown");
        breakdown.GetProperty("star1").GetInt32().Should().Be(0);
        breakdown.GetProperty("star2").GetInt32().Should().Be(0);
        breakdown.GetProperty("star3").GetInt32().Should().Be(0);
        breakdown.GetProperty("star4").GetInt32().Should().Be(0);
        breakdown.GetProperty("star5").GetInt32().Should().Be(0);
    }

    private static async Task<Guid> SeedToolkitAsync(
        MeepleAiDbContext dbContext,
        Guid authorId,
        string name,
        bool isPublished,
        TemplateStatus templateStatus)
    {
        // Mirror the Phase 4a seed pattern (raw SQL — bypasses the strict EF
        // entity which has private setters and required JSON columns).
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Game for {name}",
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.SharedGames.Add(game);
        await dbContext.SaveChangesAsync();

        var toolkitId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var templateStatusInt = (int)templateStatus;
        var isTemplate = templateStatus == TemplateStatus.Approved;

        const string sql = """
            INSERT INTO "GameToolkits" (
                "Id", "GameId", "PrivateGameId", "Name", "Version",
                "CreatedByUserId", "IsPublished",
                "OverridesTurnOrder", "OverridesScoreboard", "OverridesDiceSet",
                "CreatedAt", "UpdatedAt",
                "DiceToolsJson", "CardToolsJson", "TimerToolsJson",
                "CounterToolsJson", "UserDicePresetsJson",
                "ScoringTemplateJson", "TurnTemplateJson", "StateTemplate",
                "AgentConfig",
                "TemplateStatus", "IsTemplate",
                "ReviewNotes", "ReviewedByUserId", "ReviewedAt"
            ) VALUES (
                {0}, {1}, NULL, {2}, 1,
                {3}, {4},
                FALSE, FALSE, FALSE,
                {5}, {6},
                NULL, NULL, NULL,
                NULL, NULL,
                NULL, NULL, NULL,
                NULL,
                {7}, {8},
                NULL, NULL, NULL
            )
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            toolkitId,
            game.Id,
            name,
            authorId,
            isPublished,
            now,
            now,
            templateStatusInt,
            isTemplate);

        return toolkitId;
    }
}
