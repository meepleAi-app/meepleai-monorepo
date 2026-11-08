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

namespace Api.Tests.Integration.GameManagement;

/// <summary>La fixture di <see cref="GameNightTimeZoneEndpointTests"/>: host per classe, database fresco per test.</summary>
public sealed class GameNightTimeZoneHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "gn_time_zone");

/// <summary>
/// #4754: il fuso IANA che il client manda con la serata arriva fino alla colonna <c>time_zone_id</c> e ci resta.
/// </summary>
/// <remarks>
/// <para>
/// Contro un PostgreSQL vero perche' e' l'unico modo di vedere le tre cose che un test unitario non vede: la
/// migrazione che aggiunge la colonna, la mappatura entita' ↔ aggregato in <c>GameNightEventRepository</c>, e
/// <c>UpdateAsync</c>, che riscrive <b>tutto</b> l'aggregato a ogni salvataggio: se il mapper dimenticasse il
/// campo, qualunque altra scrittura (pubblicare, modificare l'orario) lo azzererebbe in silenzio.
/// </para>
/// <para>
/// La colonna si legge direttamente dalla tabella: il DTO di lettura non espone il fuso, che serve solo
/// alle email.
/// </para>
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4754")]
public sealed class GameNightTimeZoneEndpointTests
    : IClassFixture<GameNightTimeZoneHostFixture>, IAsyncLifetime
{
    private readonly GameNightTimeZoneHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GameNightTimeZoneEndpointTests(GameNightTimeZoneHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        _client = _hostFixture.Client;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Create_WithATimeZone_StoresIt()
    {
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Europe/Rome"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await TimeZoneOfAsync(id)).Should().Be("Europe/Rome");
    }

    [Fact]
    public async Task Create_WithoutATimeZone_StoresNone_LikeAnOlderClient()
    {
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, timeZoneId: null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await TimeZoneOfAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Create_WithAMalformedTimeZone_IsRefusedAndSavesNothing()
    {
        var (organizerId, token) = await CreateUserAsync();
        var id = Guid.NewGuid();

        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Europe/Rome; DROP TABLE game_night_events"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CountGameNightsAsync(organizerId)).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithAWellFormedZoneNoHostKnows_StillCreatesTheNight()
    {
        // The zone only feeds a display; the email says UTC when it cannot use it. Refusing to create the
        // night over it would turn a cosmetic problem into a lost event.
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Mars/Olympus"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await TimeZoneOfAsync(id)).Should().Be("Mars/Olympus");
    }

    [Fact]
    public async Task Publish_KeepsTheTimeZone_BecauseUpdateRewritesTheWholeAggregate()
    {
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();
        (await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Europe/Rome")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var publish = await SendAsync(HttpMethod.Post, $"/api/v1/game-nights/{id}/publish", token, new { });

        publish.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        (await StatusOfAsync(id)).Should().Be("Published", "the fixture needs a write that is not the zone's own");
        (await TimeZoneOfAsync(id)).Should().Be("Europe/Rome");
    }

    [Fact]
    public async Task Update_WithoutATimeZone_KeepsTheStoredOne()
    {
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();
        (await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Europe/Rome")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // An older build moves the time and knows nothing of the field.
        var update = await SendAsync(HttpMethod.Put, $"/api/v1/game-nights/{id}", token, UpdateBody(timeZoneId: null));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TimeZoneOfAsync(id)).Should().Be("Europe/Rome");
    }

    [Fact]
    public async Task Update_WithATimeZone_ReplacesIt()
    {
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();
        (await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Europe/Rome")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var update = await SendAsync(HttpMethod.Put, $"/api/v1/game-nights/{id}", token, UpdateBody("America/New_York"));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TimeZoneOfAsync(id)).Should().Be("America/New_York");
    }

    [Fact]
    public async Task Update_WithAMalformedTimeZone_IsRefusedLikeThePost_AndLeavesTheStoredOne()
    {
        var (_, token) = await CreateUserAsync();
        var id = Guid.NewGuid();
        (await SendAsync(HttpMethod.Post, "/api/v1/game-nights", token, NewBody(id, "Europe/Rome")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var update = await SendAsync(HttpMethod.Put, $"/api/v1/game-nights/{id}", token, UpdateBody("not a zone!"));

        update.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TimeZoneOfAsync(id)).Should().Be("Europe/Rome");
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private async Task<(Guid UserId, string Token)> CreateUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateUserSessionAsync(dbContext);
    }

    private static object NewBody(Guid id, string? timeZoneId) => new
    {
        id,
        title = "Serata di prova",
        scheduledAt = DateTimeOffset.UtcNow.AddDays(3),
        timeZoneId,
    };

    private static object UpdateBody(string? timeZoneId) => new
    {
        title = "Serata di prova, spostata",
        scheduledAt = DateTimeOffset.UtcNow.AddDays(4),
        timeZoneId,
    };

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string sessionToken, object body) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(method, url, sessionToken, body));

    private async Task<string?> TimeZoneOfAsync(Guid gameNightId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await dbContext.GameNightEvents
            .Where(e => e.Id == gameNightId)
            .Select(e => e.TimeZoneId)
            .SingleAsync();
    }

    private async Task<string> StatusOfAsync(Guid gameNightId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await dbContext.GameNightEvents
            .Where(e => e.Id == gameNightId)
            .Select(e => e.Status)
            .SingleAsync();
    }

    private async Task<int> CountGameNightsAsync(Guid organizerId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await dbContext.GameNightEvents.CountAsync(e => e.OrganizerId == organizerId);
    }
}
