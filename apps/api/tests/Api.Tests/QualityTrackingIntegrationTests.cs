using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD integration tests for quality tracking system.
/// These tests verify end-to-end flow with real PostgreSQL database (TDD RED phase).
///
/// Infrastructure: Uses Testcontainers PostgreSQL + WebApplicationFactory
/// Authentication: Session cookie-based authentication via /api/v1/auth/login
/// Isolation: Each test creates unique users to prevent session ID collisions
/// </summary>
public class QualityTrackingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgresContainer;
    private readonly bool _isRunningInCi;
    private string _connectionString;
    private WebApplicationFactory<Program> _factory = null!;
    private int _userCounter = 0;

    public QualityTrackingIntegrationTests()
    {
        // Detect CI environment
        _isRunningInCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (_isRunningInCi)
        {
            // In CI: Use service container from environment variable
            _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
                               "Host=localhost;Port=5432;Database=meepleai_test;Username=meeple;Password=meeplepass";
            _postgresContainer = null;
        }
        else
        {
            // Local: Use Testcontainers
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("meepleai_quality_test")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .WithCleanUp(true)
                .Build();
            _connectionString = string.Empty; // Will be set after container starts
        }
    }

    public async Task InitializeAsync()
    {
        if (!_isRunningInCi && _postgresContainer != null)
        {
            // Start PostgreSQL container (local development only)
            await _postgresContainer.StartAsync();
            _connectionString = _postgresContainer.GetConnectionString();
        }

        // Create WebApplicationFactory configured with PostgreSQL + mocked services
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext and external service dependencies
                    var descriptors = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<MeepleAiDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(MeepleAiDbContext) ||
                        d.ServiceType == typeof(Api.Services.IQdrantService) ||
                        d.ServiceType == typeof(Api.Services.IEmbeddingService)
                    ).ToList();

                    foreach (var descriptor in descriptors)
                    {
                        services.Remove(descriptor);
                    }

                    // Add DbContext with Testcontainers PostgreSQL connection string
                    services.AddDbContext<MeepleAiDbContext>(options =>
                    {
                        options.UseNpgsql(_connectionString);
                        options.EnableServiceProviderCaching(false);
                    });

                    // Mock Qdrant service (returns realistic search results for quality scoring)
                    var mockQdrantService = new Moq.Mock<Api.Services.IQdrantService>();
                    mockQdrantService
                        .Setup(x => x.SearchAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<float[]>(),
                            Moq.It.IsAny<int>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(Api.Services.SearchResult.CreateSuccess(new List<Api.Services.SearchResultItem>
                        {
                            new Api.Services.SearchResultItem
                            {
                                Score = 0.92f,
                                Text = "This is a relevant rulebook passage about winning conditions. To win the game, a player must achieve the objective.",
                                PdfId = Guid.NewGuid().ToString(),
                                Page = 5,
                                ChunkIndex = 1
                            },
                            new Api.Services.SearchResultItem
                            {
                                Score = 0.88f,
                                Text = "Another relevant passage explaining game mechanics and victory conditions in detail.",
                                PdfId = Guid.NewGuid().ToString(),
                                Page = 6,
                                ChunkIndex = 2
                            },
                            new Api.Services.SearchResultItem
                            {
                                Score = 0.85f,
                                Text = "Additional context about the rules and how to determine the winner of the match.",
                                PdfId = Guid.NewGuid().ToString(),
                                Page = 7,
                                ChunkIndex = 3
                            }
                        }));

                    // Also mock the language-specific overload
                    mockQdrantService
                        .Setup(x => x.SearchAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<float[]>(),
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<int>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(Api.Services.SearchResult.CreateSuccess(new List<Api.Services.SearchResultItem>
                        {
                            new Api.Services.SearchResultItem
                            {
                                Score = 0.92f,
                                Text = "This is a relevant rulebook passage about winning conditions. To win the game, a player must achieve the objective.",
                                PdfId = Guid.NewGuid().ToString(),
                                Page = 5,
                                ChunkIndex = 1
                            },
                            new Api.Services.SearchResultItem
                            {
                                Score = 0.88f,
                                Text = "Another relevant passage explaining game mechanics and victory conditions in detail.",
                                PdfId = Guid.NewGuid().ToString(),
                                Page = 6,
                                ChunkIndex = 2
                            },
                            new Api.Services.SearchResultItem
                            {
                                Score = 0.85f,
                                Text = "Additional context about the rules and how to determine the winner of the match.",
                                PdfId = Guid.NewGuid().ToString(),
                                Page = 7,
                                ChunkIndex = 3
                            }
                        }));
                    services.AddSingleton(mockQdrantService.Object);

                    // Mock Embedding service (tests don't need real embeddings)
                    var mockEmbeddingService = new Moq.Mock<Api.Services.IEmbeddingService>();
                    mockEmbeddingService
                        .Setup(x => x.GenerateEmbeddingAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(new Api.Services.EmbeddingResult
                        {
                            Success = true,
                            Embeddings = new List<float[]> { new float[768] }
                        });
                    services.AddSingleton(mockEmbeddingService.Object);

                    // Mock LLM service (returns high-quality responses for testing)
                    var mockLlmService = new Moq.Mock<Api.Services.ILlmService>();
                    mockLlmService
                        .Setup(x => x.GenerateCompletionAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(Api.Services.LlmCompletionResult.CreateSuccess(
                            response: "To win the game, you must fulfill the victory conditions specified in the rulebook. " +
                                     "This typically involves achieving a specific objective, such as reaching a target score, " +
                                     "eliminating all opponents, or completing a designated goal. The exact winning conditions " +
                                     "depend on the specific game you are playing. Refer to the game's rulebook for detailed " +
                                     "information about how to achieve victory in your particular game.",
                            usage: new Api.Services.LlmUsage(PromptTokens: 150, CompletionTokens: 75, TotalTokens: 225),
                            metadata: new Dictionary<string, string>
                            {
                                ["model"] = "mock-model",
                                ["finish_reason"] = "stop"
                            }
                        ));
                    services.AddSingleton(mockLlmService.Object);
                });
            });

        // Apply migrations to create schema
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Dispose factory
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        // Stop and dispose container (local development only)
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates an authenticated HTTP client with session cookie.
    /// Each call creates a unique user to prevent session ID collisions between tests.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClientAsync(string emailPrefix, UserRole role = UserRole.User)
    {
        var uniqueId = Interlocked.Increment(ref _userCounter);
        var email = $"{emailPrefix}-{uniqueId}@quality-test.local";
        var password = "TestPassword123!";

        // Create test user in database
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = new UserEntity
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                PasswordHash = HashPassword(password),
                DisplayName = $"Quality Test User {uniqueId}",
                Role = role,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        // Authenticate via login endpoint to get session cookie
        var loginClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var loginPayload = new { email, password };
        var loginResponse = await loginClient.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        loginResponse.EnsureSuccessStatusCode();

        // Extract session cookie
        if (!loginResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues))
        {
            throw new InvalidOperationException("Login did not return session cookie");
        }

        var sessionCookie = cookieValues
            .Select(value => value.Split(';')[0])
            .FirstOrDefault(c => c.StartsWith("meeple_session="));

        if (sessionCookie == null)
        {
            throw new InvalidOperationException("Session cookie not found in login response");
        }

        // Create authenticated client with session cookie
        var authenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        authenticatedClient.DefaultRequestHeaders.Add("Cookie", sessionCookie);

        return authenticatedClient;
    }

    /// <summary>
    /// Hashes a password using PBKDF2 (matches AuthService implementation).
    /// </summary>
    private static string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Property to access factory's service provider (for database queries in tests)
    /// </summary>
    private IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Scenario: Low-quality response flagged and logged to database
    /// Given a Q&A request that produces low-quality response (RAG 0.40, overall 0.45)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = true
    /// </summary>
    [Fact]
    public async Task QaEndpoint_LowQualityResponse_LoggedToDatabase()
    {
        // Given: Authenticated user
        var client = await CreateAuthenticatedClientAsync("qa-user-low", UserRole.User);

        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "What is this rule?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Query database for logged request
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logs = dbContext.AiRequestLogs
            .Where(log => log.IsLowQuality)
            .ToList();

        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.True(log.IsLowQuality);
        Assert.True(log.OverallConfidence < 0.60);
    }

    /// <summary>
    /// Scenario: High-quality response not flagged
    /// Given a Q&A request that produces high-quality response (RAG 0.85, overall 0.87)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = false
    /// </summary>
    [Fact]
    public async Task QaEndpoint_HighQualityResponse_NotFlagged()
    {
        // Given: Authenticated user
        var client = await CreateAuthenticatedClientAsync("qa-user-high", UserRole.User);

        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "How do I win?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Query database
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logs = dbContext.AiRequestLogs
            .OrderByDescending(log => log.CreatedAt)
            .First();

        Assert.False(logs.IsLowQuality);
        Assert.True(logs.OverallConfidence >= 0.60);
    }

    /// <summary>
    /// Scenario: Quality scores stored in database
    /// Given a Q&A request
    /// When response is generated
    /// Then all quality scores (RAG, LLM, Citation, Overall) should be stored
    /// </summary>
    [Fact]
    public async Task QaEndpoint_QualityScores_StoredInDatabase()
    {
        // Given: Authenticated user
        var client = await CreateAuthenticatedClientAsync("qa-user-scores", UserRole.User);

        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "Test query"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var log = dbContext.AiRequestLogs
            .OrderByDescending(l => l.CreatedAt)
            .First();

        Assert.NotNull(log.RagConfidence);
        Assert.InRange(log.RagConfidence.Value, 0.0, 1.0);
        Assert.NotNull(log.LlmConfidence);
        Assert.InRange(log.LlmConfidence.Value, 0.0, 1.0);
        Assert.NotNull(log.CitationQuality);
        Assert.InRange(log.CitationQuality.Value, 0.0, 1.0);
        Assert.NotNull(log.OverallConfidence);
        Assert.InRange(log.OverallConfidence.Value, 0.0, 1.0);
    }

    /// <summary>
    /// Scenario: Admin endpoint returns low-quality responses
    /// Given 5 responses logged (2 low-quality, 3 high-quality)
    /// When admin calls GET /admin/quality/low-responses
    /// Then response should return only the 2 low-quality entries
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_GetLowQualityResponses_ReturnsOnlyLowQuality()
    {
        // Given: Admin user is authenticated
        var client = await CreateAuthenticatedClientAsync("quality-admin-list", UserRole.Admin);

        // Arrange - Seed database with mixed quality responses
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = new[]
            {
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 1",
                    RagConfidence = 0.40,
                    LlmConfidence = 0.45,
                    CitationQuality = 0.50,
                    OverallConfidence = 0.45,
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 2",
                    RagConfidence = 0.85,
                    LlmConfidence = 0.87,
                    CitationQuality = 0.90,
                    OverallConfidence = 0.87,
                    IsLowQuality = false
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 3",
                    RagConfidence = 0.35,
                    LlmConfidence = 0.40,
                    CitationQuality = 0.45,
                    OverallConfidence = 0.40,
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 4",
                    RagConfidence = 0.80,
                    LlmConfidence = 0.82,
                    CitationQuality = 0.85,
                    OverallConfidence = 0.82,
                    IsLowQuality = false
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 5",
                    RagConfidence = 0.75,
                    LlmConfidence = 0.78,
                    CitationQuality = 0.80,
                    OverallConfidence = 0.78,
                    IsLowQuality = false
                }
            };
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/low-responses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Responses, r => Assert.True(r.IsLowQuality));
    }

    /// <summary>
    /// Scenario: Admin endpoint pagination
    /// Given 25 low-quality responses in database
    /// When admin calls GET /admin/quality/low-responses?limit=10&offset=0
    /// Then response should return first 10 entries
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_Pagination_ReturnsCorrectPage()
    {
        // Given: Admin user is authenticated
        var client = await CreateAuthenticatedClientAsync("quality-admin-pagination", UserRole.Admin);

        // Arrange - Seed 25 low-quality responses
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = Enumerable.Range(1, 25).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                Endpoint = "qa",
                Status = "Success",
                Query = $"Query {i}",
                RagConfidence = 0.40,
                LlmConfidence = 0.45,
                CitationQuality = 0.50,
                OverallConfidence = 0.45,
                IsLowQuality = true
            }).ToArray();
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/low-responses?limit=10&offset=0");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        Assert.NotNull(result);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Responses.Count);
    }

    /// <summary>
    /// Scenario: Non-admin user forbidden from admin endpoint
    /// Given authenticated non-admin user
    /// When user calls GET /admin/quality/low-responses
    /// Then response should be 403 Forbidden
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_NonAdminUser_ReturnsForbidden()
    {
        // Given: Regular user (non-admin) is authenticated
        var client = await CreateAuthenticatedClientAsync("qa-user-forbidden", UserRole.User);

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/low-responses");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated request to admin endpoint
    /// Given unauthenticated request
    /// When calling GET /admin/quality/low-responses
    /// Then response should be 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_Unauthenticated_ReturnsUnauthorized()
    {
        // Given: Unauthenticated client (no cookies)
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/low-responses");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Admin quality report endpoint
    /// Given 50 responses logged over 7 days
    /// When admin calls GET /admin/quality/report?days=7
    /// Then response should include statistics (total, low-quality count, averages)
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_QualityReport_ReturnsStatistics()
    {
        // Given: Admin user is authenticated
        var client = await CreateAuthenticatedClientAsync("quality-admin-report", UserRole.Admin);

        // Arrange - Seed responses
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = Enumerable.Range(1, 50).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddDays(-i / 7.0),
                Endpoint = "qa",
                Status = "Success",
                Query = $"Query {i}",
                RagConfidence = i <= 15 ? 0.40 : 0.80,
                LlmConfidence = i <= 15 ? 0.45 : 0.82,
                CitationQuality = i <= 15 ? 0.50 : 0.85,
                OverallConfidence = i <= 15 ? 0.45 : 0.82,
                IsLowQuality = i <= 15
            }).ToArray();
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/report?days=7");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<QualityReport>();
        Assert.NotNull(report);
        Assert.Equal(50, report.TotalResponses);
        Assert.Equal(15, report.LowQualityCount);
        Assert.InRange(report.AverageRagConfidence!.Value, 0.60, 0.75);
        Assert.InRange(report.AverageOverallConfidence!.Value, 0.60, 0.75);
    }

    /// <summary>
    /// Scenario: Filter low-quality responses by date range
    /// Given responses logged over 30 days
    /// When admin calls GET /admin/quality/low-responses?startDate=2025-01-01&endDate=2025-01-07
    /// Then response should only include responses within date range
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_DateRangeFilter_ReturnsFilteredResults()
    {
        // Given: Admin user is authenticated
        var client = await CreateAuthenticatedClientAsync("quality-admin-datefilter", UserRole.Admin);

        // Arrange
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = new[]
            {
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2024, 12, 25, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Old query",
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "In range query 1",
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "In range query 2",
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Future query",
                    IsLowQuality = true
                }
            };
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/low-responses?startDate=2025-01-01&endDate=2025-01-07");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Responses, r =>
        {
            Assert.True(r.CreatedAt >= new DateTime(2025, 1, 1));
            Assert.True(r.CreatedAt <= new DateTime(2025, 1, 7));
        });
    }

    /// <summary>
    /// Scenario: Concurrent quality tracking
    /// Given 10 simultaneous Q&A requests
    /// When all requests are processed
    /// Then all 10 responses should be logged with correct quality scores (no race conditions)
    /// </summary>
    [Fact]
    public async Task QaEndpoint_ConcurrentRequests_AllLogged()
    {
        // Given: Authenticated user
        var client = await CreateAuthenticatedClientAsync("qa-user-concurrent", UserRole.User);

        // Arrange
        var requests = Enumerable.Range(1, 10).Select(i => new
        {
            gameId = Guid.NewGuid(),
            query = $"Concurrent query {i}"
        }).ToList();

        // Act
        var tasks = requests.Select(req =>
            client.PostAsJsonAsync("/api/v1/agents/qa", req)
        ).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logCount = dbContext.AiRequestLogs.Count();

        Assert.True(logCount >= 10, "All concurrent requests should be logged");
    }
}

#region Models (these will fail compilation - expected in RED phase)

/// <summary>
/// Result model for low-quality responses endpoint.
/// </summary>
public class LowQualityResponsesResult
{
    public int TotalCount { get; set; }
    public List<LowQualityResponseDto> Responses { get; set; } = new();
}

/// <summary>
/// DTO for low-quality response.
/// </summary>
public class LowQualityResponseDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Query { get; set; } = string.Empty;
    public double RagConfidence { get; set; }
    public double LlmConfidence { get; set; }
    public double CitationQuality { get; set; }
    public double OverallConfidence { get; set; }
    public bool IsLowQuality { get; set; }
}

/// <summary>
/// Quality report model for admin dashboard.
/// </summary>
public class QualityReport
{
    public int TotalResponses { get; set; }
    public int LowQualityCount { get; set; }
    public double? AverageRagConfidence { get; set; }
    public double? AverageLlmConfidence { get; set; }
    public double? AverageCitationQuality { get; set; }
    public double? AverageOverallConfidence { get; set; }
}

#endregion
