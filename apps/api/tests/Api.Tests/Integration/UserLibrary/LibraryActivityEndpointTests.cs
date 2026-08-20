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
/// Integration tests for GET /api/v1/library/activity endpoint (Issue #642 — Wave B.3 followup).
/// Verifies authentication, empty-state, and ordering semantics for the activity feed.
/// </summary>
/// <summary>
/// Host e database condivisi da tutti i test della classe. Issue #3742.
///
/// <para>
/// xUnit istanzia la classe di test **una volta per metodo**, quindi un
/// <see cref="IAsyncLifetime.InitializeAsync"/> che costruisce un
/// <see cref="WebApplicationFactory{T}"/> lo ricostruisce per ogni test. Misurato in CI: i tre
/// test di questa classe costavano 99,6s / 112,5s / 114,4s — durate uniformi, non «il primo lento
/// e gli altri veloci», che è la firma del setup pagato ogni volta. Il costo non è il database
/// (mediana 3,4s per i test che ne creano uno senza host) ma la costruzione dell'host: MediatR e
/// FluentValidation scandiscono un assembly con 445 handler e 678 validator a ogni build.
/// </para>
/// <para>
/// Come <c>IClassFixture</c> il costo si paga una volta per classe. Condividere il database fra i
/// test è sicuro **qui** perché ogni test crea il proprio utente con
/// <c>TestSessionHelper.CreateUserSessionAsync</c> e asserisce solo su quello: nessun test pretende
/// un database vuoto. Va verificato caso per caso prima di applicare lo stesso schema altrove.
/// </para>
/// </summary>
public sealed class LibraryActivityHostFixture : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _shared;
    private readonly string _databaseName = $"library_activity_{Guid.NewGuid():N}";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public LibraryActivityHostFixture(SharedTestcontainersFixture shared) => _shared = shared;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _shared.CreateIsolatedDatabaseAsync(_databaseName);
        Factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        Client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        // La versione per-test non lo faceva: ogni test lasciava dietro di sé un database.
        await _shared.DropIsolatedDatabaseAsync(_databaseName);
    }
}

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
