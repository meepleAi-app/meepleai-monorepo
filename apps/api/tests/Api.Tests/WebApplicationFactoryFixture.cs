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
        });

        builder.UseEnvironment("Testing");
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

    public WebApplicationFactoryFixture WithTestServices(Action<IServiceCollection> configureServices)
    {
        return (WebApplicationFactoryFixture)WithWebHostBuilder(builder =>
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
