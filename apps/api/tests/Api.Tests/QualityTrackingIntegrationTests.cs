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
using FluentAssertions;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    private readonly PostgreSqlContainer? _postgresContainer;
    private readonly bool _isRunningInCi;
    private string _connectionString;
    private WebApplicationFactory<Program> _factory = null!;
    private int _userCounter = 0;

    public QualityTrackingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
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
                        d.ServiceType == typeof(Api.Services.IEmbeddingService) ||
                        d.ServiceType == typeof(Api.Services.IAiResponseCacheService) ||
                        d.ServiceType == typeof(Api.Services.IPromptTemplateService) // AI-11: Mock PromptTemplateService dependency
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
                    // Returns low-quality results if gameId starts with 0-4, high-quality if starts with 5-9/a-f
                    var mockQdrantService = new Moq.Mock<Api.Services.IQdrantService>();

                    // Mock non-language overload
                    mockQdrantService
                        .Setup(x => x.SearchAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<float[]>(),
                            Moq.It.IsAny<int>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync((string gameId, float[] embedding, int limit, System.Threading.CancellationToken ct) =>
                        {
                            // Use gameId to determine quality (50% chance low, 50% high based on first character)
                            bool isLowQuality = gameId.Length > 0 && "0123456789".Contains(gameId[0]) && gameId[0] < '5';
                            return CreateMockSearchResult(isLowQuality);
                        });

                    // Mock language-specific overload
                    mockQdrantService
                        .Setup(x => x.SearchAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<float[]>(),
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<int>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync((string gameId, float[] embedding, string language, int limit, System.Threading.CancellationToken ct) =>
                        {
                            // Use gameId to determine quality (50% chance low, 50% high based on first character)
                            // GameIds starting with 0-4 are low-quality, 5-9/a-f are high-quality
                            var firstChar = char.ToLowerInvariant(gameId[0]);
                            bool isLowQuality = gameId.Length > 0 && "0123456789abcdef".Contains(firstChar) && firstChar < '5';
                            return CreateMockSearchResult(isLowQuality);
                        });

                    // Helper method to create mock search results based on quality level
                    Api.Services.SearchResult CreateMockSearchResult(bool isLowQuality)
                    {
                        if (isLowQuality)
                        {
                            return Api.Services.SearchResult.CreateSuccess(new List<Api.Services.SearchResultItem>
                            {
                                new Api.Services.SearchResultItem
                                {
                                    Score = 0.35f,
                                    Text = "Vague text from rulebook.",
                                    PdfId = Guid.NewGuid().ToString(),
                                    Page = 1,
                                    ChunkIndex = 1
                                },
                                new Api.Services.SearchResultItem
                                {
                                    Score = 0.40f,
                                    Text = "Another unclear passage.",
                                    PdfId = Guid.NewGuid().ToString(),
                                    Page = 2,
                                    ChunkIndex = 2
                                },
                                new Api.Services.SearchResultItem
                                {
                                    Score = 0.45f,
                                    Text = "Somewhat related content.",
                                    PdfId = Guid.NewGuid().ToString(),
                                    Page = 3,
                                    ChunkIndex = 3
                                }
                            });
                        }
                        else
                        {
                            return Api.Services.SearchResult.CreateSuccess(new List<Api.Services.SearchResultItem>
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
                            });
                        }
                    }

                    services.AddSingleton(mockQdrantService.Object);

                    // Mock Embedding service (tests don't need real embeddings)
                    var mockEmbeddingService = new Moq.Mock<Api.Services.IEmbeddingService>();
                    // Mock both 2-parameter and 3-parameter (with language) overloads
                    mockEmbeddingService
                        .Setup(x => x.GenerateEmbeddingAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(new Api.Services.EmbeddingResult
                        {
                            Success = true,
                            Embeddings = new List<float[]> { new float[768] }
                        });
                    mockEmbeddingService
                        .Setup(x => x.GenerateEmbeddingAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<string>(), // language parameter
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync(new Api.Services.EmbeddingResult
                        {
                            Success = true,
                            Embeddings = new List<float[]> { new float[768] }
                        });
                    services.AddSingleton(mockEmbeddingService.Object);

                    // Mock LLM service (returns short response ONLY for specific low-quality test gameId)
                    // Returns long responses by default to avoid breaking other tests
                    var mockLlmService = new Moq.Mock<Api.Services.ILlmService>();
                    mockLlmService
                        .Setup(x => x.GenerateCompletionAsync(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync((string systemPrompt, string userPrompt, System.Threading.CancellationToken ct) =>
                        {
                            // Detect low-quality RAG context by checking for low-quality snippet text markers
                            // Low-quality RAG snippets (returned for gameIds starting with 0-4) contain these phrases
                            bool isLowQualityContext = userPrompt.Contains("Vague text from rulebook") ||
                                                       userPrompt.Contains("Another unclear passage") ||
                                                       userPrompt.Contains("Somewhat related content");

                            if (isLowQualityContext)
                            {
                                // Very short response (3 words < 50 word threshold) with hedging phrases
                                // Triggers VeryShortPenalty (0.30) + hedging penalties (0.10)
                                // Expected LLM confidence: 0.85 - 0.30 - 0.10 = 0.45
                                // With RAG=0.40, Citation=1.00: Overall = (0.40 × 0.40) + (0.45 × 0.40) + (1.00 × 0.20) = 0.54 < 0.60 ✓
                                return Api.Services.LlmCompletionResult.CreateSuccess(
                                    response: "Not sure. Unclear.",  // 3 words → VeryShortPenalty + hedging
                                    usage: new Api.Services.LlmUsage(PromptTokens: 50, CompletionTokens: 20, TotalTokens: 70),
                                    metadata: new Dictionary<string, string>
                                    {
                                        ["model"] = "mock-model",
                                        ["finish_reason"] = "stop"
                                    }
                                );
                            }
                            else
                            {
                                // Long, high-quality response (>100 words) has no penalties
                                // Expected LLM confidence: 0.85
                                // With high-quality RAG (0.88), Citation=1.00: Overall = (0.88 × 0.40) + (0.85 × 0.40) + (1.00 × 0.20) = 0.89 > 0.60 ✓
                                return Api.Services.LlmCompletionResult.CreateSuccess(
                                    response: "To win the game, you must fulfill the victory conditions specified in the rulebook. " +
                                             "The victory conditions are clearly defined and establish the criteria for determining the winner. " +
                                             "These conditions involve achieving specific objectives that demonstrate mastery of the game mechanics. " +
                                             "Common victory conditions include reaching a target score through strategic gameplay, " +
                                             "eliminating all opponents through tactical decisions, completing designated goals within the time limit, " +
                                             "or fulfilling special requirements unique to each game. The rulebook provides comprehensive details " +
                                             "about these conditions, ensuring all players understand the path to victory before starting.",
                                    usage: new Api.Services.LlmUsage(PromptTokens: 150, CompletionTokens: 120, TotalTokens: 270),
                                    metadata: new Dictionary<string, string>
                                    {
                                        ["model"] = "mock-model",
                                        ["finish_reason"] = "stop"
                                    }
                                );
                            }
                        });
                    services.AddSingleton(mockLlmService.Object);

                    // Mock cache service (always returns null to force cache misses and use mocked LLM)
                    var mockCacheService = new Moq.Mock<Api.Services.IAiResponseCacheService>();
                    // Setup GetAsync to return null for any type T (cache miss)
                    mockCacheService
                        .Setup(x => x.GetAsync<Api.Models.QaResponse>(
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<System.Threading.CancellationToken>()))
                        .ReturnsAsync((Api.Models.QaResponse?)null);
                    services.AddSingleton(mockCacheService.Object);

                    // AI-11: Mock PromptTemplateService (required by RagService after AI-07.1)
                    var mockPromptService = new Moq.Mock<Api.Services.IPromptTemplateService>();
                    mockPromptService
                        .Setup(x => x.ClassifyQuestion(Moq.It.IsAny<string>()))
                        .Returns(Api.Models.QuestionType.General); // Default classification
                    mockPromptService
                        .Setup(x => x.GetTemplateAsync(
                            Moq.It.IsAny<Guid?>(),
                            Moq.It.IsAny<Api.Models.QuestionType>()))
                        .ReturnsAsync(new Api.Models.PromptTemplate
                        {
                            SystemPrompt = "You are a helpful assistant.",
                            UserPromptTemplate = "Context: {context}\n\nQuestion: {question}",
                            FewShotExamples = new List<Api.Models.FewShotExample>()
                        });
                    mockPromptService
                        .Setup(x => x.RenderSystemPrompt(Moq.It.IsAny<Api.Models.PromptTemplate>()))
                        .Returns("You are a helpful assistant.");
                    mockPromptService
                        .Setup(x => x.RenderUserPrompt(
                            Moq.It.IsAny<Api.Models.PromptTemplate>(),
                            Moq.It.IsAny<string>(),
                            Moq.It.IsAny<string>()))
                        .Returns((Api.Models.PromptTemplate _, string context, string question) =>
                            $"Context: {context}\n\nQuestion: {question}");
                    services.AddSingleton(mockPromptService.Object);
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

        // Register creates user and returns session cookie automatically
        var registerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var registerPayload = new
        {
            email,
            password,
            displayName = $"Quality Test User {uniqueId}"
        };

        var registerResponse = await registerClient.PostAsJsonAsync("/api/v1/auth/register", registerPayload);
        registerResponse.EnsureSuccessStatusCode();

        // Extract session cookie from register response
        if (!registerResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues))
        {
            throw new InvalidOperationException("Register did not return session cookie");
        }

        var sessionCookie = cookieValues
            .Select(value => value.Split(';')[0])
            .FirstOrDefault(c => c.StartsWith("meeple_session="));

        if (sessionCookie == null)
        {
            throw new InvalidOperationException("Session cookie not found in register response");
        }

        // Promote user to desired role if not User
        if (role != UserRole.User)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    user.Role = role;
                    await dbContext.SaveChangesAsync();
                }
            }
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
        // Use specific GUID that starts with '0' to trigger low-quality mock response
        var request = new
        {
            gameId = new Guid("00000000-0000-0000-0000-000000000001"),
            query = "What is this rule?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Query database for logged request
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // AI-11: Get most recent log (should be from this test request)
        var log = await dbContext.AiRequestLogs
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        // Verify log was created
        log.Should().NotBeNull();

        // Verify quality scores were calculated and stored
        log.RagConfidence.Should().NotBeNull();
        log.LlmConfidence.Should().NotBeNull();
        log.CitationQuality.Should().NotBeNull();
        log.OverallConfidence.Should().NotBeNull();

        // Verify low-quality detection
        log.IsLowQuality, $"Expected IsLowQuality = true, but got false. Overall confidence: {log.OverallConfidence:F3}".Should().BeTrue();
        log.OverallConfidence < 0.60, $"Expected OverallConfidence < 0.60, but got {log.OverallConfidence:F3}".Should().BeTrue();

        // Verify individual score components are in expected ranges
        log.RagConfidence.Value.Should().BeInRange(0.35, 0.45); // Average of low-quality RAG scores (0.35, 0.40, 0.45)
        log.LlmConfidence.Value.Should().BeInRange(0.45, 0.55); // Base 0.85 - VeryShortPenalty 0.30 - hedging ~0.05
        log.CitationQuality.Value, 2.Should().Be(1.0); // 3 citations / 1 paragraph = 1.0
    }

    /// <summary>
    /// Scenario: High-quality response not flagged
    /// Given a Q&A request that produces high-quality response (RAG 0.88, overall 0.89)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = false
    /// </summary>
    [Fact]
    public async Task QaEndpoint_HighQualityResponse_NotFlagged()
    {
        // Given: Authenticated user
        var client = await CreateAuthenticatedClientAsync("qa-user-high", UserRole.User);

        // Arrange
        // Use specific GUID that starts with '5' or higher to trigger high-quality mock response
        var request = new
        {
            gameId = new Guid("50000000-0000-0000-0000-000000000001"),
            query = "How do I win?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Query database
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logs = dbContext.AiRequestLogs
            .OrderByDescending(log => log.CreatedAt)
            .First();

        logs.IsLowQuality.Should().BeFalse();
        logs.OverallConfidence >= 0.60.Should().BeTrue();
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
        response.IsSuccessStatusCode.Should().BeTrue();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var log = dbContext.AiRequestLogs
            .OrderByDescending(l => l.CreatedAt)
            .First();

        log.RagConfidence.Should().NotBeNull();
        log.RagConfidence.Value.Should().BeInRange(0.0, 1.0);
        log.LlmConfidence.Should().NotBeNull();
        log.LlmConfidence.Value.Should().BeInRange(0.0, 1.0);
        log.CitationQuality.Should().NotBeNull();
        log.CitationQuality.Value.Should().BeInRange(0.0, 1.0);
        log.OverallConfidence.Should().NotBeNull();
        log.OverallConfidence.Value.Should().BeInRange(0.0, 1.0);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Responses.Should().OnlyContain(r => r.IsLowQuality);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(25);
        result.Responses.Count.Should().Be(10);
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
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
            // Create 50 records distributed over 6.5 days (well within 7-day window)
            // This ensures all records are captured even with timing differences
            var logs = Enumerable.Range(1, 50).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddDays(-i / 7.7), // Use 7.7 instead of 7.0 to stay within 7-day window
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<QualityReport>();
        report.Should().NotBeNull();
        report.TotalResponses.Should().Be(50);
        report.LowQualityCount.Should().Be(15);
        report.AverageRagConfidence!.Value.Should().BeInRange(0.60, 0.75);
        report.AverageOverallConfidence!.Value.Should().BeInRange(0.60, 0.75);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        Assert.All(result.Responses, r =>
        {
            r.CreatedAt >= new DateTime(2025, 1, 1).Should().BeTrue();
            r.CreatedAt <= new DateTime(2025, 1, 7).Should().BeTrue();
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
        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logCount = dbContext.AiRequestLogs.Count();

        logCount >= 10, "All concurrent requests should be logged".Should().BeTrue();
    }
}
