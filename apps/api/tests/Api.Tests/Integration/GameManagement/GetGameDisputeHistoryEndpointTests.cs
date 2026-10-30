using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche' e le condizioni stanno in
/// <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// Condividere il database e' sicuro QUI: ogni test semina il proprio gioco (id nuovo) e la propria sessione, e la
/// rotta sotto prova filtra per quel <c>GameId</c>. Nessuna asserzione legge un elenco globale.
/// </para>
/// </summary>
public sealed class GetGameDisputeHistoryHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "game_dispute_hist");

/// <summary>
/// #4769 — <c>GET /api/v1/games/{gameId}/dispute-history</c> e' per gli amministratori. Prima bastava essere
/// autenticati, e la rotta restituiva descrizioni, verdetti, riferimenti e <c>RaisedByPlayerName</c> di tutte le
/// sessioni di quel gioco: anche di sessioni a cui chi chiama non partecipa, con verdetti che da #4768 derivano dal
/// regolamento indicizzato. La rotta per sessione (<c>GET /live-sessions/{sessionId}/disputes</c>, solo
/// partecipanti: <see cref="GetSessionDisputesEndpointTests"/>) e' quella che l'applicazione usa.
/// </summary>
/// <remarks>
/// La stessa garanzia ha un test unitario sulla tabella di routing vera
/// (<c>GameDisputeHistoryAuthorizationTests</c>, nel cancello delle PR) e una voce nella sonda
/// <c>NoDuplicateRouteTests.EveryAdminRoute_RejectsAPlainUser</c>. Qui si prova con sessioni e database veri.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4769")]
public sealed class GetGameDisputeHistoryEndpointTests : IClassFixture<GetGameDisputeHistoryHostFixture>
{
    private const string Verdict = "No - one card per turn (#4769 verdict, must not leak).";
    private const string RaisedBy = "Alice-4769";

    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Come scrive il mapper, cosi' il DisputesJson seminato si rilegge cosi' com'e'.
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GetGameDisputeHistoryEndpointTests(GetGameDisputeHistoryHostFixture host)
    {
        _factory = host.Factory;
    }

    [Fact(DisplayName = "An admin who does not take part in the sessions reads the game's dispute history")]
    public async Task Admin_ReadsTheDisputesOfEverySessionOfTheGame()
    {
        var seeded = await SeedGameWithOneDisputeAsync();
        var adminToken = await CreateAdminTokenAsync();

        var response = await SendAsync(seeded.GameId, adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetGameDisputeHistoryResult>(JsonOptions);
        result.Should().NotBeNull();
        result!.GameId.Should().Be(seeded.GameId);
        result.TotalDisputes.Should().Be(1);
        var session = result.Sessions.Should().ContainSingle().Subject;
        session.SessionId.Should().Be(seeded.SessionId);
        var dispute = session.Disputes.Should().ContainSingle().Subject;
        dispute.Verdict.Should().Be(Verdict);
        dispute.RaisedByPlayerName.Should().Be(RaisedBy);
    }

    [Fact(DisplayName = "An authenticated user who does not take part in the sessions gets 403 and no verdict")]
    public async Task PlainUser_NotInTheSessions_Gets403_AndTheBodyCarriesNoVerdict()
    {
        var seeded = await SeedGameWithOneDisputeAsync();
        var strangerToken = await CreatePlainUserTokenAsync();

        var response = await SendAsync(seeded.GameId, strangerToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(Verdict);
        body.Should().NotContain(RaisedBy);
    }

    [Fact(DisplayName = "Even the creator of the session gets 403: the per-session route is the one for participants")]
    public async Task PlainUser_WhoCreatedTheSession_Gets403()
    {
        var seeded = await SeedGameWithOneDisputeAsync();

        var response = await SendAsync(seeded.GameId, seeded.OwnerToken);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the cross-session route is admin-only; a participant reads their own session's disputes from /live-sessions/{id}/disputes");
    }

    [Fact(DisplayName = "An anonymous caller gets 401")]
    public async Task Anonymous_Gets401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/games/{Guid.NewGuid()}/dispute-history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(Guid gameId, string token)
    {
        using var client = _factory.CreateClient();
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/games/{gameId}/dispute-history", token);
        return await client.SendAsync(request);
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(db);
        return token;
    }

    private async Task<string> CreatePlainUserTokenAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);
        return token;
    }

    /// <summary>
    /// Un gioco del catalogo e una sessione live di un utente normale su quel gioco, con una disputa. Il
    /// <c>DisputesJson</c> si scrive con una UPDATE diretta (come in <see cref="GetSessionDisputesEndpointTests"/>):
    /// niente flusso a piu' passi dell'Arbitro, e si controlla che la riga scritta sia una.
    /// </summary>
    private async Task<(Guid GameId, Guid SessionId, string OwnerToken)> SeedGameWithOneDisputeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Dispute History {Guid.NewGuid():N}");
        var (ownerId, ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: ownerId,
            GameName: "Dispute History Game",
            GameId: gameId));

        var dispute = new RuleDisputeEntry(
            Guid.NewGuid(), "Can I play two cards?", Verdict,
            new List<string> { "p.12" }, RaisedBy, DateTime.UtcNow.AddMinutes(-5));
        var json = JsonSerializer.Serialize(new List<RuleDisputeEntry> { dispute }, CamelCaseOptions);
        var affected = await db.LiveGameSessions
            .Where(e => e.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DisputesJson, json));
        affected.Should().Be(1, "the seed must update exactly the target session's DisputesJson");

        return (gameId, sessionId, ownerToken);
    }
}
