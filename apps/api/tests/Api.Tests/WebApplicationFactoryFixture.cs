using System.Net;
using System.Net.Http;
using System.Text;
using System;
using Api.Infrastructure;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Api.Tests;

/// <summary>
/// Test fixture for creating a test server with in-memory database
/// </summary>
public class WebApplicationFactoryFixture : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["N8N_ENCRYPTION_KEY"] = "integration-test-encryption-key"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove real services
            // IMPORTANT: Remove ALL DbContext-related services to prevent dual database provider registration
            // EF Core registers multiple services when AddDbContext is called, and we need to remove all of them
            // to avoid the "multiple database providers registered" error
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<MeepleAiDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(MeepleAiDbContext) ||
                d.ServiceType == typeof(IConnectionMultiplexer) ||
                d.ServiceType == typeof(QdrantService) ||
                d.ServiceType == typeof(IQdrantService) ||
                d.ServiceType == typeof(IQdrantClientAdapter) ||
                d.ServiceType == typeof(IEmbeddingService)
            ).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Create and open connection (keep it open for the lifetime of the factory)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add test database with the persistent connection
            services.AddDbContext<MeepleAiDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Mock Redis with proper script evaluation support
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();

            // Mock ScriptEvaluateAsync to return valid rate limit results: [allowed=1, tokens_remaining=100, retry_after=0]
            var rateLimitResult = RedisResult.Create(new[] {
                RedisResult.Create((RedisValue)1),
                RedisResult.Create((RedisValue)100),
                RedisResult.Create((RedisValue)0)
            });

            mockDatabase.Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(rateLimitResult);

            mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // Mock Qdrant client adapter to avoid network calls in tests
            var mockQdrantAdapter = new Mock<IQdrantClientAdapter>();

            // Mock collection listing - return empty list initially, collection will be "created"
            mockQdrantAdapter
                .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "meepleai_documents" }.AsReadOnly());

            // Mock collection creation - succeed silently
            mockQdrantAdapter
                .Setup(x => x.CreateCollectionAsync(
                    It.IsAny<string>(),
                    It.IsAny<Qdrant.Client.Grpc.VectorParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Mock payload index creation - succeed silently
            mockQdrantAdapter
                .Setup(x => x.CreatePayloadIndexAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Qdrant.Client.Grpc.PayloadSchemaType>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Mock upsert operation - store points in memory
            mockQdrantAdapter
                .Setup(x => x.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<Qdrant.Client.Grpc.PointStruct>>(),
                    It.IsAny<CancellationToken>()))
                .Callback((string collectionName, IReadOnlyList<Qdrant.Client.Grpc.PointStruct> points, CancellationToken ct) =>
                {
                    lock (_storageLock)
                    {
                        if (!_mockQdrantStorage.ContainsKey(collectionName))
                        {
                            _mockQdrantStorage[collectionName] = new List<Qdrant.Client.Grpc.PointStruct>();
                        }

                        // Remove existing points with same IDs (upsert behavior)
                        var existingIds = new HashSet<string>(points.Select(p => p.Id.Uuid));
                        _mockQdrantStorage[collectionName].RemoveAll(p => existingIds.Contains(p.Id.Uuid));

                        // Add new points
                        _mockQdrantStorage[collectionName].AddRange(points);
                    }
                })
                .Returns(Task.CompletedTask);

            // Mock search operation - return filtered results from in-memory storage
            mockQdrantAdapter
                .Setup(x => x.SearchAsync(
                    It.IsAny<string>(),
                    It.IsAny<float[]>(),
                    It.IsAny<Qdrant.Client.Grpc.Filter>(),
                    It.IsAny<ulong?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string collectionName, float[] vector, Qdrant.Client.Grpc.Filter? filter, ulong? limit, CancellationToken ct) =>
                {
                    lock (_storageLock)
                    {
                        if (!_mockQdrantStorage.ContainsKey(collectionName))
                        {
                            return new List<Qdrant.Client.Grpc.ScoredPoint>().AsReadOnly();
                        }

                        var points = _mockQdrantStorage[collectionName];

                        // Apply filter if provided
                        var filteredPoints = points.AsEnumerable();

                        if (filter != null && filter.Must.Count > 0)
                        {
                            foreach (var condition in filter.Must)
                            {
                                if (condition.Field != null)
                                {
                                    var fieldKey = condition.Field.Key;
                                    var matchValue = condition.Field.Match?.Keyword;

                                    if (!string.IsNullOrEmpty(fieldKey) && !string.IsNullOrEmpty(matchValue))
                                    {
                                        filteredPoints = filteredPoints.Where(p =>
                                            p.Payload.ContainsKey(fieldKey) &&
                                            p.Payload[fieldKey].StringValue == matchValue);
                                    }
                                }
                            }
                        }

                        // Convert to scored points (use high default score since we're not doing real similarity)
                        var scoredPoints = filteredPoints
                            .Select((p, index) => new Qdrant.Client.Grpc.ScoredPoint
                            {
                                Id = p.Id,
                                Payload = { p.Payload },
                                Score = 0.95f - (index * 0.01f) // Decreasing scores for ranking
                            })
                            .Take((int)(limit ?? 10))
                            .ToList();

                        return scoredPoints.AsReadOnly();
                    }
                });

            // Mock delete operation - remove points from in-memory storage
            mockQdrantAdapter
                .Setup(x => x.DeleteAsync(
                    It.IsAny<string>(),
                    It.IsAny<Qdrant.Client.Grpc.Filter>(),
                    It.IsAny<CancellationToken>()))
                .Callback((string collectionName, Qdrant.Client.Grpc.Filter filter, CancellationToken ct) =>
                {
                    lock (_storageLock)
                    {
                        if (!_mockQdrantStorage.ContainsKey(collectionName))
                        {
                            return;
                        }

                        // Apply filter to find points to delete
                        if (filter != null && filter.Must.Count > 0)
                        {
                            foreach (var condition in filter.Must)
                            {
                                if (condition.Field != null)
                                {
                                    var fieldKey = condition.Field.Key;
                                    var matchValue = condition.Field.Match?.Keyword;

                                    if (!string.IsNullOrEmpty(fieldKey) && !string.IsNullOrEmpty(matchValue))
                                    {
                                        _mockQdrantStorage[collectionName].RemoveAll(p =>
                                            p.Payload.ContainsKey(fieldKey) &&
                                            p.Payload[fieldKey].StringValue == matchValue);
                                    }
                                }
                            }
                        }
                    }
                })
                .Returns(Task.CompletedTask);

            services.AddSingleton(mockQdrantAdapter.Object);
            services.AddSingleton<IQdrantService>(sp => new QdrantService(
                sp.GetRequiredService<IQdrantClientAdapter>(),
                sp.GetRequiredService<ILogger<QdrantService>>()
            ));

            // Mock embedding service to return dummy embeddings (1536 dimensions for text-embedding-3-small)
            var mockEmbeddingService = new Mock<IEmbeddingService>();
            mockEmbeddingService
                .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<string> texts, CancellationToken ct) =>
                {
                    // Return dummy embeddings (1536 dimensions of zeros) for each text
                    var embeddings = texts.Select(_ => new float[1536]).ToList();
                    return new Api.Services.EmbeddingResult
                    {
                        Success = true,
                        Embeddings = embeddings
                    };
                });

            mockEmbeddingService
                .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string text, CancellationToken ct) =>
                {
                    // Return dummy embedding (1536 dimensions of zeros)
                    return new Api.Services.EmbeddingResult
                    {
                        Success = true,
                        Embeddings = new List<float[]> { new float[1536] }
                    };
                });

            services.AddSingleton(mockEmbeddingService.Object);

            services.AddSingleton<IHttpClientFactory>(_ => new SmartHttpClientFactory());
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure database is created and seeded after host is built
        if (!_databaseInitialized)
        {
            lock (_databaseInitLock)
            {
                if (!_databaseInitialized)
                {
                    using var scope = host.Services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                    db.Database.EnsureCreated();

                    // Seed demo data (since EnsureCreated doesn't run migrations)
                    SeedDemoData(db);

                    _databaseInitialized = true;
                }
            }
        }

        return host;
    }

    private static bool _databaseInitialized = false;
    private static readonly object _databaseInitLock = new object();

    private static void SeedDemoData(MeepleAiDbContext db)
    {
        // Pre-computed password hash for "Demo123!" using PBKDF2 (210k iterations, SHA256)
        const string demoPasswordHash = "v1.210000.7wX9YqJ4hN5mK3pL6rT8vQ==.ug8xD+xho0bq+7hmTiefZgaOxYFu8rbdLjm7sDq6WGA=";
        var now = new DateTime(2025, 10, 9, 14, 0, 0, DateTimeKind.Utc);

        // Check if seed data already exists (idempotent)
        if (db.Users.Any(u => u.Email.EndsWith("@meepleai.dev")))
        {
            return; // Already seeded
        }

        // Seed demo users
        var adminUser = new Api.Infrastructure.Entities.UserEntity
        {
            Id = "demo-admin-001",
            Email = "admin@meepleai.dev",
            DisplayName = "Demo Admin",
            PasswordHash = demoPasswordHash,
            Role = Api.Infrastructure.Entities.UserRole.Admin,
            CreatedAt = now
        };

        var editorUser = new Api.Infrastructure.Entities.UserEntity
        {
            Id = "demo-editor-001",
            Email = "editor@meepleai.dev",
            DisplayName = "Demo Editor",
            PasswordHash = demoPasswordHash,
            Role = Api.Infrastructure.Entities.UserRole.Editor,
            CreatedAt = now
        };

        var regularUser = new Api.Infrastructure.Entities.UserEntity
        {
            Id = "demo-user-001",
            Email = "user@meepleai.dev",
            DisplayName = "Demo User",
            PasswordHash = demoPasswordHash,
            Role = Api.Infrastructure.Entities.UserRole.User,
            CreatedAt = now
        };

        db.Users.AddRange(adminUser, editorUser, regularUser);

        // Seed demo games
        var ticTacToeGame = new Api.Infrastructure.Entities.GameEntity
        {
            Id = "tic-tac-toe",
            Name = "Tic-Tac-Toe",
            CreatedAt = now
        };

        var chessGame = new Api.Infrastructure.Entities.GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = now
        };

        db.Games.AddRange(ticTacToeGame, chessGame);

        // Seed demo rule specs
        var ticTacToeRuleSpec = new Api.Infrastructure.Entities.RuleSpecEntity
        {
            Id = Guid.Parse("f5e4d3c2-b1a0-4f3e-9d8c-7b6a5e4d3c21"),
            GameId = "tic-tac-toe",
            Version = "v1.0",
            CreatedAt = now,
            CreatedByUserId = "demo-admin-001"
        };

        var chessRuleSpec = new Api.Infrastructure.Entities.RuleSpecEntity
        {
            Id = Guid.Parse("a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6"),
            GameId = "chess",
            Version = "v1.0",
            CreatedAt = now,
            CreatedByUserId = "demo-admin-001"
        };

        db.RuleSpecs.AddRange(ticTacToeRuleSpec, chessRuleSpec);

        // Seed demo agents
        var tttExplainAgent = new Api.Infrastructure.Entities.AgentEntity
        {
            Id = "agent-ttt-explain",
            GameId = "tic-tac-toe",
            Name = "Tic-Tac-Toe Explainer",
            Kind = "explain",
            CreatedAt = now
        };

        var tttQaAgent = new Api.Infrastructure.Entities.AgentEntity
        {
            Id = "agent-ttt-qa",
            GameId = "tic-tac-toe",
            Name = "Tic-Tac-Toe Q&A",
            Kind = "qa",
            CreatedAt = now
        };

        var chessExplainAgent = new Api.Infrastructure.Entities.AgentEntity
        {
            Id = "agent-chess-explain",
            GameId = "chess",
            Name = "Chess Explainer",
            Kind = "explain",
            CreatedAt = now
        };

        var chessQaAgent = new Api.Infrastructure.Entities.AgentEntity
        {
            Id = "agent-chess-qa",
            GameId = "chess",
            Name = "Chess Q&A",
            Kind = "qa",
            CreatedAt = now
        };

        db.Agents.AddRange(tttExplainAgent, tttQaAgent, chessExplainAgent, chessQaAgent);

        // Save all changes
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// In-memory storage for mocked Qdrant points across all tests
    /// Key: collection name, Value: list of points
    /// </summary>
    private static readonly Dictionary<string, List<Qdrant.Client.Grpc.PointStruct>> _mockQdrantStorage = new();
    private static readonly object _storageLock = new object();

    /// <summary>
    /// Smart HTTP client factory that returns realistic LLM responses for testing
    /// </summary>
    private sealed class SmartHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new SmartLlmHandler())
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// HTTP handler that simulates OpenRouter LLM API responses based on request content
        /// </summary>
        private sealed class SmartLlmHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Read request body to determine what kind of response to return
                var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

                // Parse the request to extract the prompt/context
                var requestJson = System.Text.Json.JsonDocument.Parse(requestBody);

                // Check if this is a chat completion request
                if (requestJson.RootElement.TryGetProperty("messages", out var messages))
                {
                    var messageArray = messages.EnumerateArray().ToList();

                    // Get the user message (usually the last one)
                    var userMessage = messageArray.LastOrDefault(m =>
                        m.TryGetProperty("role", out var role) && role.GetString() == "user");

                    string userContent = "";
                    if (userMessage.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                        userMessage.TryGetProperty("content", out var content))
                    {
                        userContent = content.GetString() ?? "";
                    }

                    // Check if this is an "explain" request (contains CONTEXT, no QUESTION)
                    bool isExplain = userContent.Contains("CONTEXT FROM RULEBOOK") &&
                                    !userContent.Contains("QUESTION:");

                    // Check if this is a Q&A request (contains both CONTEXT and QUESTION)
                    bool isQa = userContent.Contains("CONTEXT FROM RULEBOOK") &&
                               userContent.Contains("QUESTION:");

                    // Check if there's no context (empty search results)
                    bool hasNoContext = userContent.Contains("CONTEXT FROM RULEBOOK") &&
                                       userContent.Contains("[Page") == false;

                    string responseText;

                    if (hasNoContext)
                    {
                        // No indexed content found - return appropriate message
                        if (isExplain)
                        {
                            responseText = "No relevant information found about this topic in the rulebook.";
                        }
                        else
                        {
                            responseText = "Not specified";
                        }
                    }
                    else if (isExplain)
                    {
                        // Generate explain response with structured format
                        // Extract topic from context if possible
                        var topicMatch = System.Text.RegularExpressions.Regex.Match(userContent, @"Explanation:\s*(.+?)\n");
                        var topic = topicMatch.Success ? topicMatch.Groups[1].Value.Trim() : "game rules";

                        // Extract context snippets
                        var contextSnippets = ExtractContextSnippets(userContent);

                        responseText = $@"# Explanation: {topic}

## Overview

{(contextSnippets.Count > 0 ? contextSnippets[0] : "This section covers the key aspects of " + topic + ".")}

## Details

{string.Join("\n\n", contextSnippets.Skip(1))}

Based on the rulebook, {topic} involves the mechanics and conditions described above. Players should refer to the specific page numbers cited for complete details.";
                    }
                    else if (isQa)
                    {
                        // Generate Q&A response based on context
                        var contextSnippets = ExtractContextSnippets(userContent);

                        if (contextSnippets.Count > 0)
                        {
                            // Use first snippet as basis for answer
                            responseText = $"{contextSnippets[0]} (see page 1 for details)";
                        }
                        else
                        {
                            responseText = "Based on the provided rulebook, the information needed to answer this question is available on the referenced pages.";
                        }
                    }
                    else
                    {
                        // Default response for other LLM requests
                        responseText = "This is a test response from the mocked LLM service.";
                    }

                    // Build OpenRouter-compatible response with token usage
                    var llmResponse = new
                    {
                        id = "chatcmpl-test-" + Guid.NewGuid().ToString("N")[..8],
                        @object = "chat.completion",
                        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        model = "anthropic/claude-3.5-sonnet",
                        choices = new[]
                        {
                            new
                            {
                                index = 0,
                                message = new
                                {
                                    role = "assistant",
                                    content = responseText
                                },
                                finish_reason = "stop"
                            }
                        },
                        usage = new
                        {
                            prompt_tokens = EstimateTokens(userContent),
                            completion_tokens = EstimateTokens(responseText),
                            total_tokens = EstimateTokens(userContent) + EstimateTokens(responseText)
                        }
                    };

                    var jsonResponse = System.Text.Json.JsonSerializer.Serialize(llmResponse);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                    };
                }

                // Default empty response for unrecognized requests
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }

            /// <summary>
            /// Extract context snippets from the prompt (text between [Page N] markers)
            /// </summary>
            private static List<string> ExtractContextSnippets(string userContent)
            {
                var snippets = new List<string>();
                var lines = userContent.Split('\n');
                var currentSnippet = new StringBuilder();
                bool inContext = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("[Page "))
                    {
                        if (currentSnippet.Length > 0)
                        {
                            snippets.Add(currentSnippet.ToString().Trim());
                            currentSnippet.Clear();
                        }
                        inContext = true;
                        continue;
                    }

                    if (line.Contains("---") || line.StartsWith("QUESTION:") || line.StartsWith("ANSWER:"))
                    {
                        if (currentSnippet.Length > 0)
                        {
                            snippets.Add(currentSnippet.ToString().Trim());
                            currentSnippet.Clear();
                        }
                        inContext = false;
                        continue;
                    }

                    if (inContext && !string.IsNullOrWhiteSpace(line))
                    {
                        currentSnippet.AppendLine(line);
                    }
                }

                if (currentSnippet.Length > 0)
                {
                    snippets.Add(currentSnippet.ToString().Trim());
                }

                return snippets;
            }

            /// <summary>
            /// Rough token estimation (1 token ≈ 4 characters for English text)
            /// </summary>
            private static int EstimateTokens(string text)
            {
                return Math.Max(1, text.Length / 4);
            }
        }
    }

    public WebApplicationFactory<Program> WithTestServices(Action<IServiceCollection> configureServices)
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(configureServices);
        });
    }

    public HttpClient CreateHttpsClient(WebApplicationFactoryClientOptions? options = null)
    {
        options ??= new WebApplicationFactoryClientOptions();
        options.HandleCookies = false;
        options.BaseAddress ??= new Uri("https://localhost");
        return CreateClient(options);
    }
}
