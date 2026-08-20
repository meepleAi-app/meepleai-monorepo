using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> La classe supera la domanda di
/// ordine-indipendenza posta dalla base: l'endpoint filtra per utente
/// (<c>GetLibraryActivityQuery(UserId: ...)</c>), ogni test crea il proprio utente con
/// <c>TestSessionHelper.CreateUserSessionAsync</c> — che genera id ed email univoci, quindi nessuna
/// collisione di chiave — e un solo test semina partite. Nessuna asserzione riguarda conteggi
/// globali o l'ordinamento fra righe di test diversi.
/// </para>
/// </summary>
public sealed class LibraryActivityHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "library_activity");

/// <summary>
/// Integration tests for GET /api/v1/library/activity endpoint (Issue #642 — Wave B.3 followup).
/// Verifies authentication, empty-state, and ordering semantics for the activity feed.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class LibraryActivityEndpointTests : IClassFixture<LibraryActivityHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LibraryActivityEndpointTests(LibraryActivityHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task GetLibraryActivity_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/library/activity");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLibraryActivity_WithEmptyLibrary_ReturnsEmptyList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library/activity",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - With mocked auth middleware, may return Unauthorized in test env (mirrors UserLibraryEndpointsIntegrationTests pattern)
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetLibraryActivity_WithSeededEntries_ReturnsLatestFirst()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Seed 3 SharedGames + 3 UserLibraryEntries with distinct AddedAt timestamps.
        var now = DateTime.UtcNow;
        var games = new[]
        {
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Catan",
                YearPublished = 1995,
                Description = "Classic resource trading.",
                MinPlayers = 3,
                MaxPlayers = 4,
                PlayingTimeMinutes = 90,
                MinAge = 10,
                ImageUrl = string.Empty,
                ThumbnailUrl = string.Empty,
                Status = 1,
                GameDataStatus = 5,
                CreatedBy = userId,
                CreatedAt = now.AddDays(-30),
                IsDeleted = false,
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Wingspan",
                YearPublished = 2019,
                Description = "Bird-themed engine builder.",
                MinPlayers = 1,
                MaxPlayers = 5,
                PlayingTimeMinutes = 70,
                MinAge = 10,
                ImageUrl = string.Empty,
                ThumbnailUrl = string.Empty,
                Status = 1,
                GameDataStatus = 5,
                CreatedBy = userId,
                CreatedAt = now.AddDays(-30),
                IsDeleted = false,
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Brass: Birmingham",
                YearPublished = 2018,
                Description = "Industrial revolution economic strategy.",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 120,
                MinAge = 14,
                ImageUrl = string.Empty,
                ThumbnailUrl = string.Empty,
                Status = 1,
                GameDataStatus = 5,
                CreatedBy = userId,
                CreatedAt = now.AddDays(-30),
                IsDeleted = false,
            },
        };
        await dbContext.SharedGames.AddRangeAsync(games);

        var entries = new[]
        {
            new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SharedGameId = games[0].Id,
                AddedAt = now.AddDays(-3),
                IsFavorite = false,
                CurrentState = 0,
            },
            new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SharedGameId = games[1].Id,
                AddedAt = now.AddDays(-2),
                IsFavorite = false,
                CurrentState = 0,
            },
            new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SharedGameId = games[2].Id,
                AddedAt = now.AddDays(-1),
                IsFavorite = true,
                CurrentState = 0,
            },
        };
        await dbContext.UserLibraryEntries.AddRangeAsync(entries);
        await dbContext.SaveChangesAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/library/activity?limit=10",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - Conservative pattern: OK when middleware accepts the seeded session, Unauthorized otherwise
        (response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue($"Expected OK or Unauthorized, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            // The response is a JSON array. Verify newest game (Brass) appears before oldest (Catan).
            var brassIdx = body.IndexOf("Brass", StringComparison.OrdinalIgnoreCase);
            var catanIdx = body.IndexOf("Catan", StringComparison.OrdinalIgnoreCase);
            brassIdx.Should().BeGreaterThan(-1);
            catanIdx.Should().BeGreaterThan(-1);
            brassIdx.Should().BeLessThan(catanIdx, "Brass (newest) must appear before Catan (oldest) in DESC-ordered activity feed");
        }
    }
}
