using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for the #3025 L1 live game-state endpoint
/// (PUT /api/v1/live-sessions/{id}/game-state), proving end-to-end the write→persist→expose path:
/// the creator's PUT round-trips onto the GET DTO's <c>gameState</c>, and a non-participant is
/// blocked with 403 by RequireLiveSessionParticipant. Per-handler behaviour is unit-tested in
/// UpdateLiveGameStateCommandHandlerTests / GetLiveSessionQueryHandlerGameStateTests.
///
/// Mirrors LiveSessionParticipantAuthzEndpointTests (Testcontainers fixture, cookie auth, IMediator setup).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "3025")]
public sealed class LiveGameStateEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"live_gamestate_{Guid.NewGuid():N}";
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;

    public LiveGameStateEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "PUT /game-state as creator persists and round-trips on GET")]
    public async Task UpdateGameState_AsCreator_PersistsAndIsReturnedByGet()
    {
        // Arrange: authenticate as the creator + create a live session.
        var (client, sessionId) = await CreateCreatorSessionAsync();

        // Act: PUT the opaque game-state.
        var put = await client.PutAsJsonAsync(
            $"/api/v1/live-sessions/{sessionId}/game-state",
            new { state = new { k = 1 } });

        // Assert: 204 + round-trips on GET under the camelCase `gameState` field.
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await client.GetFromJsonAsync<JsonElement>($"/api/v1/live-sessions/{sessionId}");
        dto.GetProperty("gameState").GetProperty("k").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "PUT /game-state returns 403 for an authenticated non-participant")]
    public async Task UpdateGameState_AsNonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateCreatorSessionAsync();
        var (clientB, tokenB) = await CreateOtherUserClientAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/live-sessions/{sessionId}/game-state", tokenB);
        request.Content = JsonContent.Create(new { state = new { k = 1 } });

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-participant must be blocked by RequireLiveSessionParticipant");
    }

    // ── Helpers (mirror LiveSessionParticipantAuthzEndpointTests) ───────────────────

    /// <summary>Seeds a user + session, creates a LiveGameSession owned by that user, returns an authenticated client.</summary>
    private async Task<(HttpClient Client, Guid SessionId)> CreateCreatorSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId, GameName: "Game State Test Game", GameId: null));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);
        return (client, sessionId);
    }

    /// <summary>Creates an authenticated client for a different user who is NOT a participant of any session.</summary>
    private async Task<(HttpClient Client, string Token)> CreateOtherUserClientAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        return (client, token);
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"gamestate-test-{userId:N}@test.local",
            DisplayName = "Game State Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return userId;
    }
}
