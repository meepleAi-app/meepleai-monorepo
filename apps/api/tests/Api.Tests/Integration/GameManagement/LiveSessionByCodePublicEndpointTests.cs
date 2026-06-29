using System.Net;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
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
/// Integration tests for the public lobby-by-code endpoint (#2590):
///   GET /api/v1/live-sessions/code/{code}/public
/// Anonymous, narrow read-only projection. Proves the guest QR-join path works without auth,
/// the body omits sensitive fields, unknown codes 404, and the old authenticated route is unchanged.
/// Mirrors LiveSessionParticipantAuthzEndpointTests (Testcontainers fixture, IMediator setup).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2590")]
public sealed class LiveSessionByCodePublicEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"live_codepublic_{Guid.NewGuid():N}";
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;

    public LiveSessionByCodePublicEndpointTests(SharedTestcontainersFixture fixture)
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

    [Fact(DisplayName = "GET /code/{code}/public returns 200 to an anonymous caller with the narrow body")]
    public async Task Anonymous_ValidCode_Returns200_NarrowBody()
    {
        var code = await CreateSessionWithCodeAsync();
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync($"/api/v1/live-sessions/code/{code}/public");

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "the public lobby endpoint must work without auth");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"gameName\"").And.Contain("Catan");
        body.Should().Contain("\"players\"");
    }

    [Fact(DisplayName = "GET /code/{code}/public body omits all sensitive fields")]
    public async Task Anonymous_ValidCode_BodyOmitsSensitiveFields()
    {
        var code = await CreateSessionWithCodeAsync();
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync($"/api/v1/live-sessions/code/{code}/public");
        var body = await resp.Content.ReadAsStringAsync();

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain("createdByUserId")
            .And.NotContain("\"userId\"")
            .And.NotContain("notes")
            .And.NotContain("roundScores")
            .And.NotContain("teams")
            .And.NotContain("visibility")
            .And.NotContain("groupId");
    }

    [Fact(DisplayName = "GET /code/{code}/public returns 404 for an unknown code")]
    public async Task UnknownCode_Returns404()
    {
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync("/api/v1/live-sessions/code/ZZZZZZ/public");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "The old GET /code/{code} (no /public) still requires auth (401 anonymous)")]
    public async Task OldRoute_StillRequiresAuth()
    {
        var code = await CreateSessionWithCodeAsync();
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync($"/api/v1/live-sessions/code/{code}");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the authenticated full-DTO route must be unchanged by #2590");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Seeds a user + a live session (game "Catan") with one linked player, returns the SessionCode.</summary>
    private async Task<string> CreateSessionWithCodeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var repo = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();

        var userId = await SeedUserAsync(db);
        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId, GameName: "Catan", GameId: null));
        await mediator.Send(new AddPlayerToLiveSessionCommand(
            sessionId, "Alice", PlayerColor.Red, userId, null, null));

        var session = await repo.GetByIdAsync(sessionId);
        return session!.SessionCode;
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"codepublic-{userId:N}@test.local",
            DisplayName = "Code Public Test User",
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
