using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameToolkit;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.ToolkitMarketplace;

/// <summary>
/// Integration tests for the Wave 3 Phase 2 toolkit marketplace endpoints
/// (Issue #805 / PR #732 §5.3.1, §5.3.2, §5.3.5):
///
/// <list type="number">
///   <item><c>GET /api/v1/toolkits/{id}</c> — detail with viewerContext, 10min ETag per-viewer</item>
///   <item><c>GET /api/v1/toolkits/{id}/versions</c> — versions list, 10min cache</item>
///   <item><c>POST /api/v1/toolkits/{id}/install</c> — idempotent install</item>
/// </list>
///
/// Per PR #732 §5.1 (Newman BC decomposition), these endpoints EXTEND the
/// existing GameToolkit BC rather than creating a new MarketplaceBC. Schema
/// reality v1 carryovers are documented inline in the handlers (Gate B):
/// installCount stub 0, ratingAverage null, currentVersion "1.0.{int}",
/// publishedAt = UpdatedAt for approved, yankedAt always null in v1.
///
/// Security boundary (PR #732 §5.2): non-author viewers must receive 404
/// when the toolkit is not yet published / approved.
///
/// Idempotency contract (PR #732 §5.3.5 Nygard): the install endpoint
/// returns 200 on every call (never 409) regardless of whether it is the
/// first or Nth call for the same (viewer, toolkit) pair.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameToolkit")]
[Trait("Wave", "3-Phase-2")]
public sealed class ToolkitMarketplaceEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ToolkitMarketplaceEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"toolkit_marketplace_{Guid.NewGuid():N}";
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

    // ──────────────────────────────────────────────────────────────────────
    //  GET /api/v1/toolkits/{toolkitId}  (PR #732 §5.3.1)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToolkitDetail_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1/toolkits/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToolkitDetail_WhenToolkitNotFound_Returns404()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{Guid.NewGuid()}",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToolkitDetail_WhenPublished_AnyAuthUser_Returns200WithViewerContext()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Public Toolkit",
            isPublished: true,
            templateStatus: TemplateStatus.Approved);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("toolkit");
        detail.GetProperty("id").GetString().Should().Be(toolkit.Id.ToString());
        detail.GetProperty("name").GetString().Should().Be("Public Toolkit");
        detail.GetProperty("authorId").GetString().Should().Be(authorId.ToString());
        // Schema reality v1 carryover (Gate B): installCount stubbed to 0.
        detail.GetProperty("installCount").GetInt32().Should().Be(0);
        // ratingAverage is null; ratingCount is 0.
        detail.GetProperty("ratingAverage").ValueKind.Should().Be(JsonValueKind.Null);
        detail.GetProperty("ratingCount").GetInt32().Should().Be(0);
        // currentVersion shape: "1.0.{int}".
        detail.GetProperty("currentVersion").GetString().Should().StartWith("1.0.");
        // publishedAt populated for approved+published toolkit.
        detail.GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.String);
        // yankedAt always null in v1.
        detail.GetProperty("yankedAt").ValueKind.Should().Be(JsonValueKind.Null);

        var viewerContext = body.GetProperty("viewerContext");
        viewerContext.GetProperty("isOwner").GetBoolean().Should().BeFalse();
        viewerContext.GetProperty("hasInstalled").GetBoolean().Should().BeFalse();
        // canRate = hasInstalled && !isOwner. With v1 stub hasInstalled=false → canRate=false.
        viewerContext.GetProperty("canRate").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ToolkitDetail_WhenOwnDraft_OwnerSeesIsOwnerTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, authorToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "My Draft",
            isPublished: false,
            templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            authorToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("viewerContext").GetProperty("isOwner").GetBoolean().Should().BeTrue();
        // publishedAt null for unpublished draft.
        body.GetProperty("toolkit").GetProperty("publishedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ToolkitDetail_WhenUnpublishedDraft_NonAuthorReceives404()
    {
        // PR #732 §5.2 security boundary — server-side enforcement.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Hidden Draft",
            isPublished: false,
            templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToolkitDetail_WhenPendingReview_NonAuthorReceives404()
    {
        // Even PendingReview does not equal Approved; non-authors cannot see.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Pending Review",
            isPublished: true,
            templateStatus: TemplateStatus.PendingReview);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToolkitDetail_OwnerStillSeesUnpublishedToolkit()
    {
        // Owners always see their own toolkits (parallel to "yanked-but-mine"
        // soft-delete owner access carved out by PR #732 §5.2).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, authorToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "My Pending",
            isPublished: true,
            templateStatus: TemplateStatus.PendingReview);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            authorToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("viewerContext").GetProperty("isOwner").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ToolkitDetail_AgentSummary_TruncatesSystemPromptTo500Chars()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var longPrompt = new string('a', 750);
        var agentConfigJson = JsonSerializer.Serialize(new
        {
            name = "TestAgent",
            systemPrompt = longPrompt,
        });

        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Toolkit With Agent",
            isPublished: true,
            templateStatus: TemplateStatus.Approved,
            agentConfig: agentConfigJson);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var agent = body.GetProperty("toolkit").GetProperty("agent");
        agent.GetProperty("name").GetString().Should().Be("TestAgent");
        agent.GetProperty("systemPromptPreview").GetString()!.Length.Should().Be(500);
    }

    [Fact]
    public async Task ToolkitDetail_PopulatesAuthorNameFromUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        // Set DisplayName on the author so the wire shape exposes a real label.
        var authorEntity = await dbContext.Users.FirstAsync(u => u.Id == authorId);
        authorEntity.DisplayName = "Marco Designer";
        authorEntity.AvatarUrl = "https://example.com/marco.jpg";
        await dbContext.SaveChangesAsync();

        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Marco's Toolkit",
            isPublished: true,
            templateStatus: TemplateStatus.Approved);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("toolkit");
        detail.GetProperty("authorName").GetString().Should().Be("Marco Designer");
        detail.GetProperty("authorAvatarUrl").GetString().Should().Be("https://example.com/marco.jpg");
    }

    // ──────────────────────────────────────────────────────────────────────
    //  GET /api/v1/toolkits/{toolkitId}/versions  (PR #732 §5.3.2)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToolkitVersions_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1/toolkits/{Guid.NewGuid()}/versions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToolkitVersions_ToolkitNotFound_Returns404()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{Guid.NewGuid()}/versions",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToolkitVersions_PublishedToolkit_Returns200WithStubV1()
    {
        // Schema reality v1 carryover (Gate B): single-row stub list.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Versioned Toolkit",
            isPublished: true,
            templateStatus: TemplateStatus.Approved);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}/versions",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        var first = items[0];
        first.GetProperty("version").GetString().Should().StartWith("1.0.");
        first.GetProperty("isCurrent").GetBoolean().Should().BeTrue();
        first.GetProperty("yankedAt").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("changelog").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ToolkitVersions_NonAuthorOnDraft_Returns404()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Draft Versions",
            isPublished: false,
            templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}/versions",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToolkitVersions_OwnerOnDraft_Returns200WithDraftChangelog()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, authorToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Owner Draft",
            isPublished: false,
            templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/toolkits/{toolkit.Id}/versions",
            authorToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        // The handler labels the stub row as "Draft — not yet published" when
        // the toolkit hasn't crossed the publish threshold yet.
        items[0].GetProperty("changelog").GetString().Should().Contain("Draft");
    }

    // ──────────────────────────────────────────────────────────────────────
    //  POST /api/v1/toolkits/{toolkitId}/install  (PR #732 §5.3.5)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InstallToolkit_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync(
            $"/api/v1/toolkits/{Guid.NewGuid()}/install",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InstallToolkit_ToolkitNotFound_Returns404()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{Guid.NewGuid()}/install",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InstallToolkit_PublishedToolkit_Returns200WithHasInstalledTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Installable Toolkit",
            isPublished: true,
            templateStatus: TemplateStatus.Approved);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{toolkit.Id}/install",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("hasInstalled").GetBoolean().Should().BeTrue();
        // Schema reality v1 carryover (Gate B): installCount stub 0.
        body.GetProperty("installCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task InstallToolkit_Idempotent_RepeatedInstallsAlwaysReturn200()
    {
        // PR #732 §5.3.5 Nygard: repeated installs must NOT raise 409.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Idempotent Toolkit",
            isPublished: true,
            templateStatus: TemplateStatus.Approved);

        // First install.
        var request1 = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{toolkit.Id}/install",
            viewerToken);
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second install (should be idempotent → still 200).
        var request2 = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{toolkit.Id}/install",
            viewerToken);
        var response2 = await _client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var body2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        body2.GetProperty("hasInstalled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task InstallToolkit_NonAuthorOnDraft_Returns404()
    {
        // PR #732 §5.2 boundary — viewers cannot install drafts.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Draft Toolkit",
            isPublished: false,
            templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{toolkit.Id}/install",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InstallToolkit_OwnerCanInstallOwnDraft()
    {
        // Owners installing their own toolkit (e.g. for testing) is allowed.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, authorToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var toolkit = await SeedToolkitAsync(
            dbContext,
            authorId: authorId,
            name: "Owner Self Install",
            isPublished: false,
            templateStatus: TemplateStatus.Draft);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/toolkits/{toolkit.Id}/install",
            authorToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a <see cref="GameToolkitEntity"/> directly via raw SQL because
    /// EF treats the <c>RowVersion</c> column as <c>IsRowVersion()</c> which
    /// strips the value from INSERT statements. The migration declares the
    /// column NOT NULL with no DB-side default, so the EF path produces a
    /// constraint violation when seeding outside the production aggregate
    /// repository (which similarly omits RowVersion but is exercised through
    /// SaveChangesAsync paths that EF rewrites server-side).
    ///
    /// Using raw SQL bypasses the EF generator and gives us full control of
    /// the IsPublished / TemplateStatus combinations needed for the security
    /// boundary tests (PR #732 §5.2). FK to <see cref="SharedGameEntity"/>
    /// (via <see cref="GameEntity"/> mapping that points to the SharedGame
    /// row) is satisfied by inserting a sibling SharedGame first.
    /// </summary>
    private static async Task<GameToolkitEntity> SeedToolkitAsync(
        MeepleAiDbContext dbContext,
        Guid authorId,
        string name,
        bool isPublished,
        TemplateStatus templateStatus,
        string? agentConfig = null)
    {
        // GameToolkitEntity.GameId references the "games" table (GameEntity),
        // NOT SharedGames. Seed a GameEntity row so the FK constraint holds.
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Game for {name}",
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        var toolkitId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;
        var templateStatusInt = (int)templateStatus;
        var isTemplate = templateStatus == TemplateStatus.Approved;

        // Raw INSERT that explicitly sets RowVersion. EF treats RowVersion as
        // store-generated under IsRowVersion(), which strips the value during
        // INSERT and produces a NOT-NULL violation when seeding outside the
        // production aggregate path. The aggregate's repository takes the same
        // path through SaveChangesAsync but EF's update pipeline rewrites the
        // RowVersion side-effect for IsRowVersion()-tagged properties — that
        // does not happen for raw entity inserts via Add() in tests.
        //
        // We embed the agentConfig literal directly when present (escaping
        // single quotes for SQL safety AND escaping curly braces because the
        // composite format-string under ExecuteSqlRawAsync interprets {n} as
        // positional placeholders) instead of using a parameter, because
        // mixing positional {N} and named @pN parameters in the same SQL
        // produces ambiguous parameter bindings under Npgsql.
        var agentConfigSql = agentConfig is null
            ? "NULL"
            : $"'{agentConfig.Replace("'", "''", StringComparison.Ordinal).Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal)}'::jsonb";

        var sql = $$"""
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
                "ReviewNotes", "ReviewedByUserId", "ReviewedAt",
                "RowVersion"
            ) VALUES (
                {0}, {1}, NULL, {2}, 1,
                {3}, {4},
                FALSE, FALSE, FALSE,
                {5}, {6},
                NULL, NULL, NULL,
                NULL, NULL,
                NULL, NULL, NULL,
                {{agentConfigSql}},
                {7}, {8},
                NULL, NULL, NULL,
                E'\\x01'
            )
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            toolkitId,
            game.Id,
            name,
            authorId,
            isPublished,
            createdAt,
            updatedAt,
            templateStatusInt,
            isTemplate);

        return new GameToolkitEntity
        {
            Id = toolkitId,
            GameId = game.Id,
            Name = name,
            Version = 1,
            CreatedByUserId = authorId,
            IsPublished = isPublished,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            TemplateStatus = templateStatusInt,
            IsTemplate = isTemplate,
            AgentConfig = agentConfig,
            RowVersion = new byte[] { 1 },
        };
    }
}
