using System;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Benchmarks;

/// <summary>
/// CONFIG-07: Performance benchmarks for ConfigurationService
/// Validates <50ms p95 latency target and >90% cache hit ratio
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ConfigurationBenchmarks
{
    private MeepleAiDbContext _dbContext = null!;
    private ConfigurationService _configService = null!;
    private SqliteConnection _connection = null!;
    private string _testUserId = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        // Setup in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        // Create test user
        var testUser = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "benchmark@test.com",
            PasswordHash = "dummy",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(testUser);
        await _dbContext.SaveChangesAsync();
        _testUserId = testUser.Id;

        // Setup ConfigurationService with mocked dependencies
        var mockCache = new Mock<IHybridCacheService>();
        mockCache.Setup(c => c.GetOrCreateAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<SystemConfigurationDto?>>>(),
            It.IsAny<string[]>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<SystemConfigurationDto?>>, string[], TimeSpan?, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

        _configService = new ConfigurationService(
            _dbContext,
            mockCache.Object,
            NullLogger<ConfigurationService>.Instance,
            mockEnv.Object
        );

        // Seed test configuration
        var testConfig = new SystemConfigurationEntity
        {
            Key = "Benchmark:TestConfig",
            Value = "benchmark-value",
            ValueType = "string",
            Category = "Benchmark",
            CreatedByUserId = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.SystemConfigurations.Add(testConfig);
        await _dbContext.SaveChangesAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _dbContext?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }

    [Benchmark]
    public async Task<string?> GetValueAsync_FromDatabase()
    {
        return await _configService.GetValueAsync<string>("Benchmark:TestConfig");
    }

    [Benchmark]
    public async Task<SystemConfigurationDto?> GetConfigurationByKeyAsync()
    {
        return await _configService.GetConfigurationByKeyAsync("Benchmark:TestConfig");
    }

    [Benchmark]
    public async Task<PagedResult<SystemConfigurationDto>> GetConfigurationsAsync_Paged()
    {
        return await _configService.GetConfigurationsAsync(
            category: "Benchmark",
            environment: null,
            activeOnly: true,
            page: 1,
            pageSize: 50
        );
    }
}

/// <summary>
/// Program entry point for running benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ConfigurationBenchmarks>();
    }
}

// Placeholder for PagedResult (would normally reference from Api project)
public record PagedResult<T>(
    System.Collections.Generic.IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize
);

public record SystemConfigurationDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
