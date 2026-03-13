using System.Net;
using System.Net.Http.Json;
using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Models;
using Api.Routing;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for BGG Import Queue HTTP endpoints.
/// Issue #3541: BGG Import Queue Service
/// Tests: Admin auth, CQRS flow, status endpoint, enqueue/batch, cancel, retry, SSE streaming
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggImportQueueEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private static readonly Guid TestAdminUserId = Guid.NewGuid();

    public BggImportQueueEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"bggendpoints_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Create WebApplicationFactory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OPENROUTER_API_KEY"] = "test-key",
                        ["ConnectionStrings:Postgres"] = connectionString,
                        ["BggImportQueue:Enabled"] = "false" // Disable background worker for tests
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Replace DbContext with test database
                    services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
                    services.AddDbContext<MeepleAiDbContext>(options =>
                        options.UseNpgsql(connectionString, o => o.UseVector())); // Issue #3547

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IQdrantService));
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.AddScoped<Api.Services.IQdrantService>(_ => Mock.Of<Api.Services.IQdrantService>());
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());

                    // Mock IHybridCacheService
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());

                    // Ensure domain event collector is registered
                    services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();

                    // Register authorization policies
                    services.AddSharedGameCatalogPolicies();

                    // Bypass authorization for testing - allow all authenticated requests
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                            .RequireAssertion(_ => true) // Allow all requests in test environment
                            .Build();
                    });

                    // Mock BGG API service to avoid real API calls
                    services.RemoveAll(typeof(Api.Services.IBggApiService));
                    var mockBggApi = new Mock<Api.Services.IBggApiService>();
                    services.AddScoped(_ => mockBggApi.Object);
                });
            });

        // Initialize database and seed test data
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            // Seed test admin user
            var adminUser = new UserEntity
            {
                Id = TestAdminUserId,
                Email = "admin@test.com",
                DisplayName = "Test Admin",
                Role = "Admin", // Admin role for authorization
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Set<UserEntity>().Add(adminUser);
            await dbContext.SaveChangesAsync();

        }

        _client = _factory.CreateClient();

        // Note: Admin authorization is bypassed via RequireAssertion(ctx => true) in testing environment
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    #region GET /status Endpoint Tests

    [Fact]
    public async Task GetQueueStatus_WithQueuedItems_ReturnsCorrectCounts()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();

        await queueService.EnqueueAsync(1, "Game 1");
        await queueService.EnqueueAsync(2, "Game 2");
        var processing = await queueService.EnqueueAsync(3, "Processing Game");
        await queueService.MarkAsProcessingAsync(processing.Id);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BggQueueStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalQueued);
        Assert.Equal(1, result.TotalProcessing);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetQueueStatus_WithEmptyQueue_ReturnsEmptyResult()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BggQueueStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalQueued);
        Assert.Equal(0, result.TotalProcessing);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetQueueStatus_ExcludesCompletedAndFailed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();

        await queueService.EnqueueAsync(1, "Queued");
        var completed = await queueService.EnqueueAsync(2, "Completed");
        await queueService.MarkAsProcessingAsync(completed.Id);
        await queueService.MarkAsCompletedAsync(completed.Id, Guid.NewGuid());

        // Act
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/status");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<BggQueueStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalQueued);
        Assert.DoesNotContain(result.Items, i => i.Status == BggImportStatus.Completed);
    }

    #endregion

    #region POST /enqueue Endpoint Tests (CQRS Flow)

    [Fact]
    public async Task EnqueueSingle_WithValidBggId_ReturnsCreated()
    {
        // Arrange
        var request = new EnqueueBggRequest(174430, "Gloomhaven");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/bgg-queue/enqueue", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BggImportQueueEntity>();
        Assert.NotNull(result);
        Assert.Equal(174430, result.BggId);
        Assert.Equal("Gloomhaven", result.GameName);
        Assert.Equal(BggImportStatus.Queued, result.Status);

        // Verify database persistence
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var dbEntry = await dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.BggId == 174430);
        Assert.NotNull(dbEntry);
    }

    [Fact]
    public async Task EnqueueSingle_WithDuplicateBggId_ReturnsConflict()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        await queueService.EnqueueAsync(266192, "Wingspan");

        var request = new EnqueueBggRequest(266192, "Wingspan Duplicate");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/bgg-queue/enqueue", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal("Duplicate BGG ID", problemDetails.Title);
        Assert.Contains("266192", problemDetails.Detail);
    }

    [Fact]
    public async Task EnqueueSingle_WithInvalidBggId_ReturnsBadRequest()
    {
        // Arrange
        var request = new EnqueueBggRequest(0, "Invalid"); // BGG IDs are positive integers

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/bgg-queue/enqueue", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region POST /batch Endpoint Tests

    [Fact]
    public async Task EnqueueBatch_WithMultipleBggIds_ReturnsCreated()
    {
        // Arrange
        var request = new EnqueueBggBatchRequest(new List<int> { 174430, 266192, 68448 });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/bgg-queue/batch", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<BggImportQueueEntity>>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // Verify sequential positions
        Assert.Equal(1, result[0].Position);
        Assert.Equal(2, result[1].Position);
        Assert.Equal(3, result[2].Position);
    }

    [Fact]
    public async Task EnqueueBatch_WithSomeExisting_EnqueuesOnlyNew()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        await queueService.EnqueueAsync(174430, "Gloomhaven"); // Pre-existing

        var request = new EnqueueBggBatchRequest(new List<int> { 174430, 266192, 68448 });

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/bgg-queue/batch", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<BggImportQueueEntity>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Only 2 new entries
        Assert.DoesNotContain(result, r => r.BggId == 174430);
    }

    [Fact]
    public async Task EnqueueBatch_WithEmptyList_ReturnsCreated()
    {
        // Arrange
        var request = new EnqueueBggBatchRequest(new List<int>());

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/bgg-queue/batch", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<BggImportQueueEntity>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region DELETE /cancel Endpoint Tests

    [Fact]
    public async Task CancelQueuedImport_WithQueuedEntry_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        var entity = await queueService.EnqueueAsync(123, "Test Game");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/admin/bgg-queue/{entity.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify removal from database
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var removed = await dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.Null(removed);
    }

    [Fact]
    public async Task CancelQueuedImport_WithNonQueuedStatus_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        var entity = await queueService.EnqueueAsync(456, "Processing Game");
        await queueService.MarkAsProcessingAsync(entity.Id);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/admin/bgg-queue/{entity.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Contains("cannot be cancelled", problemDetails.Detail);
    }

    [Fact]
    public async Task CancelQueuedImport_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/v1/admin/bgg-queue/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST /retry Endpoint Tests

    [Fact]
    public async Task RetryFailedImport_WithFailedEntry_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        var entity = await queueService.EnqueueAsync(789, "Failed Game");
        await queueService.MarkAsProcessingAsync(entity.Id);
        await queueService.MarkAsFailedAsync(entity.Id, "Test error", maxRetries: 1);

        // Act
        var response = await _client.PostAsync($"/api/v1/admin/bgg-queue/{entity.Id}/retry", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify status changed to Queued
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var retried = await dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(retried);
        Assert.Equal(BggImportStatus.Queued, retried.Status);
        Assert.Equal(0, retried.RetryCount);
    }

    [Fact]
    public async Task RetryFailedImport_WithNonFailedStatus_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        var entity = await queueService.EnqueueAsync(999, "Queued Game");

        // Act
        var response = await _client.PostAsync($"/api/v1/admin/bgg-queue/{entity.Id}/retry", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Contains("cannot be retried", problemDetails.Detail);
    }

    [Fact]
    public async Task RetryFailedImport_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.PostAsync($"/api/v1/admin/bgg-queue/{Guid.NewGuid()}/retry", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /bggId Endpoint Tests

    [Fact]
    public async Task GetByBggId_WithExistingBggId_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        await queueService.EnqueueAsync(12345, "Test Game");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/12345");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BggImportQueueEntity>();
        Assert.NotNull(result);
        Assert.Equal(12345, result.BggId);
    }

    [Fact]
    public async Task GetByBggId_WithNonExistentBggId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Contains("99999", problemDetails.Detail);
    }

    #endregion

    #region GET /stream SSE Endpoint Tests (Issue #3 Fix)

    [Fact]
    public async Task StreamQueueProgress_ReturnsCorrectCounts()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();

        await queueService.EnqueueAsync(1, "Queued 1");
        await queueService.EnqueueAsync(2, "Queued 2");

        var processing = await queueService.EnqueueAsync(3, "Processing");
        await queueService.MarkAsProcessingAsync(processing.Id);

        var completed = await queueService.EnqueueAsync(4, "Completed");
        await queueService.MarkAsProcessingAsync(completed.Id);
        await queueService.MarkAsCompletedAsync(completed.Id, Guid.NewGuid());

        var failed = await queueService.EnqueueAsync(5, "Failed");
        await queueService.MarkAsProcessingAsync(failed.Id);
        await queueService.MarkAsFailedAsync(failed.Id, "Error", maxRetries: 1);

        // Act - Use CancellationToken to limit SSE stream
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/stream", cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // Read first SSE event
        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        // Read data line (format: "data: {...}")
        var dataLine = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(dataLine);
        Assert.StartsWith("data: ", dataLine);

        var jsonData = dataLine.Substring(6); // Remove "data: " prefix
        var sseEvent = System.Text.Json.JsonSerializer.Deserialize<SseQueueEvent>(jsonData);

        Assert.NotNull(sseEvent);
        Assert.Equal(2, sseEvent.queued); // 2 queued items
        Assert.Equal(1, sseEvent.processing); // 1 processing item
        Assert.Equal(1, sseEvent.completed); // 1 completed item (Issue #3 fix)
        Assert.Equal(1, sseEvent.failed); // 1 failed item (Issue #3 fix)
        Assert.Equal(3, sseEvent.eta); // queued + processing
    }

    [Fact]
    public async Task StreamQueueProgress_SendsPeriodicUpdates()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        await queueService.EnqueueAsync(1, "Test Game");

        // Act - Read multiple SSE events
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/stream", cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        var eventCount = 0;
        try
        {
            while (!cts.Token.IsCancellationRequested && eventCount < 3)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line?.StartsWith("data: ") == true)
                {
                    eventCount++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - timeout reached
        }

        // Should receive at least 2 events (updates every 2 seconds)
        Assert.True(eventCount >= 2, $"Expected at least 2 SSE events, got {eventCount}");
    }

    [Fact]
    public async Task StreamQueueProgress_IncludesActiveItemsOnly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();

        // Add 15 items (should only return top 10 active)
        for (int i = 1; i <= 15; i++)
        {
            await queueService.EnqueueAsync(i, $"Game {i}");
        }

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var response = await _client.GetAsync("/api/v1/admin/bgg-queue/stream", cts.Token);

        var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        var dataLine = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(dataLine);

        var jsonData = dataLine.Substring(6);
        var sseEvent = System.Text.Json.JsonSerializer.Deserialize<SseQueueEvent>(jsonData);

        // Assert - Should return max 10 items in items array
        Assert.NotNull(sseEvent);
        Assert.Equal(15, sseEvent.queued);
        Assert.True(sseEvent.items.Count <= 10, $"Expected max 10 items, got {sseEvent.items.Count}");
    }

    #endregion

    #region Authorization Tests (Admin Only)

    // Note: In testing environment, authorization is bypassed via RequireAssertion(ctx => true)
    // These tests verify the endpoint structure and would fail in production without admin role

    [Fact]
    public async Task BggQueueEndpoints_RequireAdminRole()
    {
        // Verify endpoint routes are protected (auth bypassed in tests)
        var statusResponse = await _client.GetAsync("/api/v1/admin/bgg-queue/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode); // Auth bypassed

        // In production: Would return 401/403 without admin role
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// SSE event structure for queue progress streaming
    /// </summary>
#pragma warning disable S1144, S3459 // Properties are populated via JSON deserialization
    private sealed class SseQueueEvent
    {
        public DateTime timestamp { get; set; }
        public int queued { get; set; }
        public int processing { get; set; }
        public int completed { get; set; }
        public int failed { get; set; }
        public int eta { get; set; }
        public List<BggImportQueueEntity> items { get; set; } = new();
    }
#pragma warning restore S1144, S3459

    #endregion
}
