using System.Net;
using System.Text;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Integration;

/// <summary>
/// La fixture di <see cref="AdminStatsCacheDateRangeIntegrationTests"/>: host e database per classe.
/// </summary>
/// <remarks>
/// Condividere il database è sicuro qui: i test asseriscono solo le date della tendenza utenti, che
/// <c>FillMissingDates</c> ricava dall'intervallo chiesto e non dalle righe presenti, e
/// <c>generatedAt</c>, che dipende solo dalla cache. Condividere l'host vuol dire condividere la cache:
/// per questo ogni test usa un <c>gameId</c> suo, che entra nella chiave.
/// </remarks>
public sealed class AdminStatsCacheDateRangeHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "admin_stats_range");

/// <summary>
/// #4542 — la cache delle statistiche admin deve distinguere l'intervallo di date.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il difetto.</b> <c>AdminStatsService.GetDashboardStatsAsync</c> usava la chiave
/// <c>dashboard:stats:{Days}:{GameId}:{RoleFilter}</c>, senza <c>fromDate</c> e <c>toDate</c>. Entro la
/// durata della cache una richiesta con un altro intervallo riceveva i dati del primo, senza errori.
/// Ne soffrivano <c>GET /admin/dashboard/stats</c> e <c>POST /admin/analytics/export</c>, che passa dallo
/// stesso metodo con <c>Days = 30</c>.
/// </para>
/// <para>
/// <b>L'osservabile.</b> <c>userTrend</c> ha un punto per ogni giorno dell'intervallo calcolato, quindi
/// la sua prima e ultima data dicono quale intervallo è stato calcolato, qualunque cosa ci sia nel
/// database. Le due richieste di ogni test hanno lo stesso <c>gameId</c>: con la chiave vecchia
/// cadevano sulla stessa voce.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4542")]
public sealed class AdminStatsCacheDateRangeIntegrationTests
    : IClassFixture<AdminStatsCacheDateRangeHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminStatsCacheDateRangeIntegrationTests(AdminStatsCacheDateRangeHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task DashboardStats_SameFiltersOtherRange_ComputesTheOtherRange()
    {
        var token = await CreateAdminSessionAsync();
        var gameId = Guid.NewGuid();

        var first = await GetStatsAsync(token, $"fromDate=2025-01-01&toDate=2025-01-03&gameId={gameId}");
        var second = await GetStatsAsync(token, $"fromDate=2025-03-01&toDate=2025-03-03&gameId={gameId}");

        UserTrendRange(first).Should().Be(("2025-01-01", "2025-01-03"));
        UserTrendRange(second).Should().Be(("2025-03-01", "2025-03-03"),
            "un intervallo diverso con gli stessi filtri è un'altra richiesta, non una voce di cache del primo");
    }

    [Fact]
    public async Task Export_AfterStatsForAnotherRange_ExportsItsOwnRange()
    {
        var token = await CreateAdminSessionAsync();
        var gameId = Guid.NewGuid();

        // Lo stats senza `days` vale Days = 30, come l'export: con la chiave vecchia era la stessa voce.
        var stats = await GetStatsAsync(token, $"fromDate=2025-01-01&toDate=2025-01-03&gameId={gameId}");
        var export = await ExportJsonAsync(token,
            $$"""{"format":"json","fromDate":"2025-03-01","toDate":"2025-03-03","gameId":"{{gameId}}"}""");

        UserTrendRange(stats).Should().Be(("2025-01-01", "2025-01-03"));
        UserTrendRange(export).Should().Be(("2025-03-01", "2025-03-03"),
            "l'export calcola l'intervallo del suo corpo, non quello chiesto prima allo stats");
    }

    /// <summary>
    /// Il controllo: la cache serve ancora. Due richieste identiche — con l'intervallo di default, che
    /// dipende dall'orologio, e con un intervallo esplicito — devono ricevere la stessa risposta
    /// (<c>generatedAt</c> uguale). Una chiave che contenesse l'istante corrente la renderebbe rossa.
    /// </summary>
    [Fact]
    public async Task DashboardStats_SameRequestTwice_IsServedFromCache()
    {
        var token = await CreateAdminSessionAsync();
        var gameId = Guid.NewGuid();

        var defaultFirst = await GetStatsAsync(token, $"gameId={gameId}");
        var defaultSecond = await GetStatsAsync(token, $"gameId={gameId}");
        var explicitFirst = await GetStatsAsync(token, $"fromDate=2025-01-01&toDate=2025-01-03&gameId={gameId}");
        var explicitSecond = await GetStatsAsync(token, $"fromDate=2025-01-01&toDate=2025-01-03&gameId={gameId}");

        GeneratedAt(defaultSecond).Should().Be(GeneratedAt(defaultFirst),
            "l'intervallo di default si misura dall'orologio, ma la sua chiave non deve contenerlo");
        GeneratedAt(explicitSecond).Should().Be(GeneratedAt(explicitFirst),
            "lo stesso intervallo esplicito è la stessa voce di cache");
        UserTrendRange(explicitSecond).Should().Be(("2025-01-01", "2025-01-03"));
    }

    // ── Aiuti ───────────────────────────────────────────────────────────────────

    private async Task<JsonElement> GetStatsAsync(string token, string query)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/admin/dashboard/stats?{query}", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<JsonElement> ExportJsonAsync(string token, string json)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/admin/analytics/export", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>La prima e l'ultima data di <c>userTrend</c>, come <c>yyyy-MM-dd</c>.</summary>
    private static (string First, string Last) UserTrendRange(JsonElement stats)
    {
        var points = stats.GetProperty("userTrend").EnumerateArray().ToList();
        points.Should().NotBeEmpty();
        return (Day(points[0]), Day(points[^1]));

        static string Day(JsonElement point) => point.GetProperty("date").GetString()![..10];
    }

    private static string GeneratedAt(JsonElement stats) => stats.GetProperty("generatedAt").GetString()!;

    private async Task<string> CreateAdminSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(db);
        return token;
    }
}
