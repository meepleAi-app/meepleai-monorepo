using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Phase4a;

/// <summary>
/// Integration tests for <c>GET /api/v1/users/top-contributors</c>
/// (Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805).
///
/// <para>
/// Powers the SP4 /discover route's "Top contributors" rail. Distinct from
/// the existing <c>/api/v1/shared-games/top-contributors</c> leaderboard
/// (sessions/wins) — this surface aggregates contribution sources (FAQs,
/// KB uploads, AI agents created) for community editors.
/// </para>
///
/// <para>
/// Schema reality v1 (Gate B audit): only <c>UploadedByUserId</c> on
/// <c>PdfDocumentEntity</c> exists. <c>GameFaqEntity</c> + <c>AgentDefinition</c>
/// have no creator FK in v1, so <c>FaqsCount</c> and <c>AgentsCreatedCount</c>
/// are stubbed to 0. <c>ContributionCount</c> therefore equals
/// <c>KbUploadsCount</c> in v1.
/// </para>
///
/// <para>
/// Privacy guards: skip suspended accounts, require <c>Status == "Active"</c>,
/// require non-null <c>DisplayName</c>.
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Wave", "3-Phase-4a")]
public sealed class TopUserContributorsEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public TopUserContributorsEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"top_user_contributors_{Guid.NewGuid():N}";
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

    [Fact]
    public async Task TopContributors_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/users/top-contributors?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TopContributors_WithEmptyData_Returns200WithEmptyItems()
    {
        // PR #732 §3.4 — empty state is 200 with { items: [] }, not 404.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task TopContributors_RanksUsersByKbUploadsDescending()
    {
        // V1: only KB uploads count toward ContributionCount. Sort: count DESC,
        // then UserId ASC tiebreak.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Seed three contributors with explicit display names + upload counts.
        var alice = await SeedActiveUserAsync(dbContext, "Alice");
        var bob = await SeedActiveUserAsync(dbContext, "Bob");
        var carol = await SeedActiveUserAsync(dbContext, "Carol");

        await SeedPdfUploadsAsync(dbContext, alice, count: 1);
        await SeedPdfUploadsAsync(dbContext, bob, count: 5);
        await SeedPdfUploadsAsync(dbContext, carol, count: 3);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
        items[0].GetProperty("displayName").GetString().Should().Be("Bob");
        items[0].GetProperty("contributionCount").GetInt32().Should().Be(5);
        items[1].GetProperty("displayName").GetString().Should().Be("Carol");
        items[1].GetProperty("contributionCount").GetInt32().Should().Be(3);
        items[2].GetProperty("displayName").GetString().Should().Be("Alice");
        items[2].GetProperty("contributionCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TopContributors_BreakdownStubsFaqsAndAgentsToZeroV1()
    {
        // Schema reality v1 (Gate B): FaqsCount + AgentsCreatedCount stubbed
        // to 0 because GameFaqEntity + AgentDefinition lack creator FK columns.
        // Only KbUploadsCount is real.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var alice = await SeedActiveUserAsync(dbContext, "Alice");
        await SeedPdfUploadsAsync(dbContext, alice, count: 4);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        var breakdown = item.GetProperty("breakdown");
        breakdown.GetProperty("faqsCount").GetInt32().Should().Be(0);
        breakdown.GetProperty("kbUploadsCount").GetInt32().Should().Be(4);
        breakdown.GetProperty("agentsCreatedCount").GetInt32().Should().Be(0);
        // ContributionCount = sum of breakdown sources (in v1: KbUploadsCount only).
        item.GetProperty("contributionCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task TopContributors_ExcludesSuspendedUsers()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var alice = await SeedActiveUserAsync(dbContext, "Alice");
        var bob = await SeedSuspendedUserAsync(dbContext, "Bob");
        await SeedPdfUploadsAsync(dbContext, alice, count: 1);
        await SeedPdfUploadsAsync(dbContext, bob, count: 10); // would dominate if not filtered

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task TopContributors_ExcludesUsersWithNullDisplayName()
    {
        // Privacy guard: avoid surfacing email-derived identifiers.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var alice = await SeedActiveUserAsync(dbContext, "Alice");
        var bob = await SeedActiveUserAsync(dbContext, displayName: null);
        await SeedPdfUploadsAsync(dbContext, alice, count: 1);
        await SeedPdfUploadsAsync(dbContext, bob, count: 10);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("displayName").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task TopContributors_RespectsLimitParameter()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        for (var i = 0; i < 5; i++)
        {
            var u = await SeedActiveUserAsync(dbContext, $"User-{i}");
            await SeedPdfUploadsAsync(dbContext, u, count: i + 1);
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=2",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task TopContributors_DtoExposesExpectedFieldShape()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var alice = await SeedActiveUserAsync(dbContext, "Alice", avatarUrl: "https://example.com/alice.png");
        await SeedPdfUploadsAsync(dbContext, alice, count: 2);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/users/top-contributors?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(alice);
        item.GetProperty("displayName").GetString().Should().Be("Alice");
        item.GetProperty("avatarUrl").GetString().Should().Be("https://example.com/alice.png");
        item.GetProperty("contributionCount").GetInt32().Should().Be(2);
        item.GetProperty("breakdown").ValueKind.Should().Be(JsonValueKind.Object);
    }

    /// <summary>
    /// Seeds an active <see cref="UserEntity"/> with the given display name.
    /// </summary>
    private static async Task<Guid> SeedActiveUserAsync(
        MeepleAiDbContext dbContext,
        string? displayName,
        string? avatarUrl = null)
    {
        var id = Guid.NewGuid();
        dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"user-{id:N}@test.com",
            DisplayName = displayName,
            PasswordHash = null,
            Role = "user",
            Tier = "free",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            Status = "Active",
            IsSuspended = false,
            AvatarUrl = avatarUrl,
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return id;
    }

    private static async Task<Guid> SeedSuspendedUserAsync(
        MeepleAiDbContext dbContext,
        string? displayName)
    {
        var id = Guid.NewGuid();
        dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"suspended-{id:N}@test.com",
            DisplayName = displayName,
            PasswordHash = null,
            Role = "user",
            Tier = "free",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            Status = "Active",   // Privacy guard fires on IsSuspended; Active+IsSuspended drops them.
            IsSuspended = true,
            SuspendedAt = DateTime.UtcNow.AddDays(-1),
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return id;
    }

    private static async Task SeedPdfUploadsAsync(
        MeepleAiDbContext dbContext,
        Guid userId,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            dbContext.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = id,
                FileName = $"doc-{i}.pdf",
                FilePath = $"/uploads/{id}.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow.AddMinutes(-i),
                ProcessingState = "Ready",
                Language = "en",
                IsActiveForRag = true
            });
        }
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }
}
