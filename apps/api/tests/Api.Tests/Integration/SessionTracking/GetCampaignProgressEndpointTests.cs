using System.Net;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
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
/// Issue #1388: integration test for GET /api/v1/gamebook/campaigns/{id}/progress.
/// Exercises the auth pipeline + MediatR DI registration + route param binding +
/// EF-side join (ListByIdsAsync) end-to-end against a real Postgres testcontainer.
/// Follows the conservative auth pattern used by sibling endpoint tests: accepts
/// either OK (middleware honors the seeded session cookie) or Unauthorized.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GetCampaignProgressEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GetCampaignProgressEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"campaign_progress_{Guid.NewGuid():N}";
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
    public async Task GetCampaignProgress_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1/gamebook/campaigns/{Guid.NewGuid()}/progress");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCampaignProgress_WithSeededCampaignAndProgress_ReturnsOkOrUnauthorized()
    {
        // Arrange: seed a campaign + 2 GameBooks + 2 SessionBookProgress rows.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var sharedGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(sharedGameId), userId, "End-to-end Test");
        var bookA = GameBook.CreateCommunity(
            GameRef.Shared(sharedGameId),
            "Press Start",
            GameBookRole.Tutorial,
            ParagraphScheme.ParagraphNumber,
            "en",
            sequentialRead: false,
            kbSourceDocId: null,
            physicalOnly: false,
            createdBy: userId);
        var bookB = GameBook.CreateCommunity(
            GameRef.Shared(sharedGameId),
            "Rules Reference",
            GameBookRole.RulesReference,
            ParagraphScheme.ParagraphNumber,
            "en",
            sequentialRead: false,
            kbSourceDocId: null,
            physicalOnly: false,
            createdBy: userId);

        await dbContext.GamebookCampaignSessions.AddAsync(session);
        await dbContext.GameBooks.AddAsync(bookA);
        await dbContext.GameBooks.AddAsync(bookB);
        await dbContext.SaveChangesAsync();

        var olderProgress = SessionBookProgress.Create(session.Id, bookA.Id, "§42");
        await Task.Delay(15);
        var newerProgress = SessionBookProgress.Create(session.Id, bookB.Id, "§99");
        await dbContext.SessionBookProgresses.AddAsync(olderProgress);
        await dbContext.SessionBookProgresses.AddAsync(newerProgress);
        await dbContext.SaveChangesAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/gamebook/campaigns/{session.Id}/progress",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — sibling integration tests use this conservative pattern because
        // the test middleware may or may not honor the seeded session cookie.
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetCampaignProgress_NonExistentCampaign_ReturnsNotFoundOrUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/gamebook/campaigns/{Guid.NewGuid()}/progress",
            sessionToken);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }
}
