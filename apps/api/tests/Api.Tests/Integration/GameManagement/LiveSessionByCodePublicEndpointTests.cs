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
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// La guardia <c>WaitForPostgresReadyAsync</c> che questa classe chiamava prima di costruire l'host
/// non e' andata persa: e' nella base, subito dopo la creazione del database isolato.
/// </para>
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test crea la propria sessione con
/// <c>CreateSessionWithCodeAsync</c> (utente nuovo, <c>CreateLiveSessionCommand</c>, codice
/// restituito dal dominio) e interroga <c>GET /code/{code}/public</c> con <b>quel</b> codice: la
/// risposta riguarda una sola sessione, non una lista. Il test del 404 usa il letterale
/// <c>ZZZZZZ</c>, che nessun test della classe semina.
/// </para>
/// </summary>
public sealed class LiveSessionByCodePublicHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "live_codepublic");

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
public sealed class LiveSessionByCodePublicEndpointTests : IClassFixture<LiveSessionByCodePublicHostFixture>
{
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public LiveSessionByCodePublicEndpointTests(LiveSessionByCodePublicHostFixture host)
    {
        _factory = host.Factory;
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
