using System.Net;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// HTTP-layer IDOR tests for the user chat-session list endpoints.
/// #3120: GET /users/{userId}/games/{gameId}/chat-sessions and
/// GET /users/{userId}/chat-sessions/recent must reject a caller whose id != {userId}
/// (mirroring the sibling GET /users/{userId}/chat-sessions/limit).
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3120")]
public sealed class ChatSessionEndpointsIdorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private readonly Guid _gameId = Guid.NewGuid();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _otherClient = null!;
    private Guid _ownerId;
    private string _ownerToken = null!;
    private string _otherToken = null!;

    public ChatSessionEndpointsIdorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"chatsession_idor_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        (_ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _otherToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        _ownerClient = _factory.CreateClient();
        _otherClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _otherClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private string UserGameSessionsUrl => $"/api/v1/users/{_ownerId}/games/{_gameId}/chat-sessions";
    private string RecentSessionsUrl => $"/api/v1/users/{_ownerId}/chat-sessions/recent";

    [Fact(Timeout = 90_000)]
    public async Task GetUserGameSessions_CallerIsOwner_Returns200()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, UserGameSessionsUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetUserGameSessions_CallerIsNotOwner_Returns403()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, UserGameSessionsUrl, _otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetRecentSessions_CallerIsOwner_Returns200()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, RecentSessionsUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetRecentSessions_CallerIsNotOwner_Returns403()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, RecentSessionsUrl, _otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
