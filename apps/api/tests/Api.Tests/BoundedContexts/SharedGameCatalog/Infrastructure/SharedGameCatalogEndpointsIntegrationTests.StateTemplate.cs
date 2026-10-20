using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// #4543 (review) — PUT e <c>activate</c> del template di stato, verificati sul database.
/// </summary>
/// <remarks>
/// <para>
/// Fino a #4543 le due rotte rispondevano 400 prima dell'handler (parametro di rotta <c>Guid _</c>).
/// Rese raggiungibili, l'attivazione rivelava il difetto dietro: disattivava gli altri template con
/// un <c>ExecuteUpdate</c> immediato, poi attivava il template scelto su un oggetto letto
/// <c>AsNoTracking</c> che nessuno salvava. Risposta 204, nessun template attivo, e
/// <c>GET …/state-template</c> 404.
/// </para>
/// <para>
/// Il segmento <c>{id}</c> ora conta: un template di un altro gioco risponde 404 e non cambia.
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    [Fact]
    public async Task ActivateStateTemplate_ActivatesItAndDeactivatesTheOthers()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var first = await SeedStateTemplateAsync(game.Id, "1.0", isActive: true);
        var second = await SeedStateTemplateAsync(game.Id, "2.0", isActive: false);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/state-template/{second.Id}/activate",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = await StateTemplateRowsAsync(game.Id);
        rows[second.Id].IsActive.Should().BeTrue("the activated template must be saved as active");
        rows[first.Id].IsActive.Should().BeFalse("only one template per game is active");

        using var get = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/admin/shared-games/{game.Id}/state-template", token);
        using var getResponse = await _client.SendAsync(get, TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, "after an activation the game has an active template");
    }

    [Fact]
    public async Task ActivateStateTemplate_OfAnotherGame_Returns404_AndChangesNothing()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var active = await SeedStateTemplateAsync(otherGame.Id, "1.0", isActive: true);
        var inactive = await SeedStateTemplateAsync(otherGame.Id, "2.0", isActive: false);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/state-template/{inactive.Id}/activate",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var rows = await StateTemplateRowsAsync(otherGame.Id);
        rows[active.Id].IsActive.Should().BeTrue();
        rows[inactive.Id].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStateTemplate_ChangesTheRow()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var template = await SeedStateTemplateAsync(game.Id, "1.0", isActive: true);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/state-template/{template.Id}",
            token,
            new { name = "Punteggi", schemaJson = """{"type":"object","properties":{"score":{"type":"integer"}}}""", version = "1.1" });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var row = (await StateTemplateRowsAsync(game.Id))[template.Id];
        row.Name.Should().Be("Punteggi");
        row.Version.Should().Be("1.1");
        row.SchemaJson.Should().Contain("score");
        row.IsActive.Should().BeTrue("an update does not change which template is active");
    }

    [Fact]
    public async Task UpdateStateTemplate_OfAnotherGame_Returns404_AndChangesNothing()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var template = await SeedStateTemplateAsync(otherGame.Id, "1.0", isActive: true);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/state-template/{template.Id}",
            token,
            new { name = "Altro", schemaJson = """{"type":"object"}""", version = "9.9" });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var row = (await StateTemplateRowsAsync(otherGame.Id))[template.Id];
        row.Name.Should().Be(template.Name);
        row.Version.Should().Be("1.0");
    }

    private async Task<GameStateTemplateEntity> SeedStateTemplateAsync(Guid gameId, string version, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var row = new GameStateTemplateEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Name = $"Template {version}",
            SchemaJson = """{"type":"object"}""",
            Version = version,
            IsActive = isActive,
            Source = 1, // Manual
            GeneratedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            CreatedBy = TestUserId,
        };
        db.GameStateTemplates.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row;
    }

    private async Task<Dictionary<Guid, GameStateTemplateEntity>> StateTemplateRowsAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.GameStateTemplates
            .AsNoTracking()
            .Where(t => t.SharedGameId == gameId)
            .ToDictionaryAsync(t => t.Id, TestContext.Current.CancellationToken);
    }
}
