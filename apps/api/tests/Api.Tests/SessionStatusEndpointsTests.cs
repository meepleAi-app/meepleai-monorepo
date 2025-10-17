using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
// TODO: Add Microsoft.Extensions.TimeProvider.Testing package
// using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Integration tests for AUTH-05 session status and extension endpoints
/// Tests /api/v1/auth/session/status and /api/v1/auth/session/extend
/// TEMPORARILY DISABLED: Requires Microsoft.Extensions.TimeProvider.Testing package
/// </summary>
/*
public class SessionStatusEndpointsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly object _timeProvider; // FakeTimeProvider

    public SessionStatusEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.Parse("2025-10-16T12:00:00Z"));

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<MeepleAiDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory SQLite
                services.AddDbContext<MeepleAiDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Replace TimeProvider with FakeTimeProvider
                services.AddSingleton<TimeProvider>(_timeProvider);

                // Remove ISessionCacheService to force database queries
                var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISessionCacheService));
                if (cacheDescriptor != null)
                {
                    services.Remove(cacheDescriptor);
                }

                // Build service provider and initialize database
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetSessionStatus_WhenAuthenticated_ReturnsCorrectMinutes()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (sessionToken, userId) = await CreateTestUserAndSession(client);
        client.DefaultRequestHeaders.Add("Cookie", $"meeple_session={sessionToken}");

        // Act
        var response = await client.GetAsync("/api/v1/auth/session/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SessionStatusResponse>();
        Assert.NotNull(status);

        // Session was just created, should have ~30 days (43200 minutes) remaining
        Assert.InRange(status.RemainingMinutes, 43000, 43300); // Allow some tolerance
        Assert.NotNull(status.LastSeenAt);
    }

    [Fact]
    public async Task GetSessionStatus_WhenUnauthenticated_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/auth/session/status");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSessionStatus_WhenSessionNearExpiry_ReturnsLowMinutes()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (sessionToken, userId) = await CreateTestUserAndSession(client);
        client.DefaultRequestHeaders.Add("Cookie", $"meeple_session={sessionToken}");

        // Advance time by 29 days and 23.5 hours (only 30 minutes remaining)
        _timeProvider.Advance(TimeSpan.FromDays(29) + TimeSpan.FromHours(23.5));

        // Act
        var response = await client.GetAsync("/api/v1/auth/session/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SessionStatusResponse>();
        Assert.NotNull(status);

        // Should have ~30 minutes remaining
        Assert.InRange(status.RemainingMinutes, 0, 35);
    }

    [Fact]
    public async Task GetSessionStatus_WhenSessionExpired_ReturnsZeroMinutes()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (sessionToken, userId) = await CreateTestUserAndSession(client);
        client.DefaultRequestHeaders.Add("Cookie", $"meeple_session={sessionToken}");

        // Advance time by more than 30 days
        _timeProvider.Advance(TimeSpan.FromDays(31));

        // Act
        var response = await client.GetAsync("/api/v1/auth/session/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SessionStatusResponse>();
        Assert.NotNull(status);

        // Session expired, should show 0 minutes
        Assert.Equal(0, status.RemainingMinutes);
    }

    [Fact]
    public async Task ExtendSession_WhenAuthenticated_UpdatesLastSeenAt()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (sessionToken, userId) = await CreateTestUserAndSession(client);
        client.DefaultRequestHeaders.Add("Cookie", $"meeple_session={sessionToken}");

        // Advance time by 1 hour
        _timeProvider.Advance(TimeSpan.FromHours(1));

        // Get initial status
        var initialResponse = await client.GetAsync("/api/v1/auth/session/status");
        var initialStatus = await initialResponse.Content.ReadFromJsonAsync<SessionStatusResponse>();

        // Act
        var extendResponse = await client.PostAsync("/api/v1/auth/session/extend", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, extendResponse.StatusCode);
        var extendedStatus = await extendResponse.Content.ReadFromJsonAsync<SessionStatusResponse>();
        Assert.NotNull(extendedStatus);

        // LastSeenAt should be updated to current time
        Assert.NotNull(extendedStatus.LastSeenAt);
        Assert.True(extendedStatus.LastSeenAt > initialStatus!.LastSeenAt);

        // Remaining minutes should be reset to ~30 days
        Assert.InRange(extendedStatus.RemainingMinutes, 43000, 43300);
    }

    [Fact]
    public async Task ExtendSession_WhenAuthenticated_InvalidatesCache()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (sessionToken, userId) = await CreateTestUserAndSession(client);
        client.DefaultRequestHeaders.Add("Cookie", $"meeple_session={sessionToken}");

        // Act
        var response = await client.PostAsync("/api/v1/auth/session/extend", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify session status reflects updated LastSeenAt
        var statusResponse = await client.GetAsync("/api/v1/auth/session/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<SessionStatusResponse>();
        Assert.NotNull(status);
        Assert.NotNull(status.LastSeenAt);

        // LastSeenAt should match the extend time (within 1 second tolerance)
        var expectedTime = _timeProvider.GetUtcNow().UtcDateTime;
        Assert.True(Math.Abs((status.LastSeenAt!.Value - expectedTime).TotalSeconds) < 1);
    }

    [Fact]
    public async Task ExtendSession_WhenUnauthenticated_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/v1/auth/session/extend", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExtendSession_MultipleTimesWithinPeriod_KeepsSessionAlive()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (sessionToken, userId) = await CreateTestUserAndSession(client);
        client.DefaultRequestHeaders.Add("Cookie", $"meeple_session={sessionToken}");

        // Act & Assert - Extend session multiple times over 60 days
        for (int i = 0; i < 3; i++)
        {
            // Advance time by 20 days
            _timeProvider.Advance(TimeSpan.FromDays(20));

            // Extend session
            var extendResponse = await client.PostAsync("/api/v1/auth/session/extend", null);
            Assert.Equal(HttpStatusCode.OK, extendResponse.StatusCode);

            // Verify session still has plenty of time remaining
            var statusResponse = await client.GetAsync("/api/v1/auth/session/status");
            var status = await statusResponse.Content.ReadFromJsonAsync<SessionStatusResponse>();
            Assert.NotNull(status);

            // After extending, should have ~30 days remaining
            Assert.InRange(status.RemainingMinutes, 42000, 43300);
        }
    }

    /// <summary>
    /// Helper to create a test user and authenticated session
    /// </summary>
    private async Task<(string sessionToken, string userId)> CreateTestUserAndSession(HttpClient client)
    {
        var registerPayload = new
        {
            Email = $"test_{Guid.NewGuid().ToString("N")}@example.com",
            Password = "TestPassword123!",
            DisplayName = "Test User"
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", registerPayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Extract session cookie
        var setCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.NotNull(setCookieHeader);

        var sessionToken = ExtractSessionTokenFromCookie(setCookieHeader);
        Assert.NotNull(sessionToken);

        var authResult = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);

        return (sessionToken, authResult.User.Id);
    }

    /// <summary>
    /// Extract session token from Set-Cookie header
    /// </summary>
    private string ExtractSessionTokenFromCookie(string setCookieHeader)
    {
        // Parse: meeple_session=<token>; Path=/; HttpOnly; SameSite=Lax
        var parts = setCookieHeader.Split(';')[0].Split('=');
        return parts.Length == 2 ? parts[1] : string.Empty;
    }
}
*/
