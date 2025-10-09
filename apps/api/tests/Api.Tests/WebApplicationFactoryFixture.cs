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
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<MeepleAiDbContext>) ||
                d.ServiceType == typeof(IConnectionMultiplexer) ||
                d.ServiceType == typeof(QdrantService) ||
                d.ServiceType == typeof(IQdrantService) ||
                d.ServiceType == typeof(IQdrantClientAdapter)
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

            // Mock Redis
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();
            mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // Mock Qdrant configuration
            var qdrantConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QDRANT_URL"] = "http://test:6333"
                })
                .Build();

            services.AddSingleton<IQdrantClientAdapter>(_ => new QdrantClientAdapter(
                qdrantConfig,
                NullLogger<QdrantClientAdapter>.Instance));
            services.AddSingleton<IQdrantService>(sp => new QdrantService(
                sp.GetRequiredService<IQdrantClientAdapter>(),
                sp.GetRequiredService<ILogger<QdrantService>>()
            ));

            services.AddSingleton<IHttpClientFactory>(_ => new StubHttpClientFactory());

            // Ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.Database.EnsureCreated();

            // Seed demo data (since EnsureCreated doesn't run migrations)
            SeedDemoData(db);
        });

        builder.UseEnvironment("Testing");
    }

    private static void SeedDemoData(MeepleAiDbContext db)
    {
        // Pre-computed password hash for "Demo123!" using PBKDF2
        const string demoPasswordHash = "v1.210000.7wX9YqJ4hN5mK3pL6rT8vW=.eH3kM8nQ7tR5xZ2wB6cV9dF4gJ1lP0sY+";
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

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHandler())
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
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
