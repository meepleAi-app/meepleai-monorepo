using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Api.Tests.Helpers;

/// <summary>
/// Helper utilities for E2E test prerequisite verification.
/// Provides graceful skipping with clear messages when required infrastructure is unavailable.
/// </summary>
/// <remarks>
/// Issue #2533: E2E Test Prerequisites Documentation and Automation
/// </remarks>
public static class E2ETestPrerequisites
{
    private const string ApiBaseUrl = "http://localhost:8080";
    private const string QdrantUrl = "http://localhost:6333";
    private const string RedisDefaultHost = "localhost";
    private const int RedisDefaultPort = 6379;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Checks if the API is running and accessible.
    /// </summary>
    /// <param name="baseUrl">API base URL (default: http://localhost:8080)</param>
    /// <returns>True if API is accessible, false otherwise</returns>
    public static async Task<bool> IsApiAvailableAsync(string? baseUrl = null)
    {
        var url = baseUrl ?? ApiBaseUrl;

        try
        {
            var response = await HttpClient.GetAsync($"{url}/health");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            // Timeout
            return false;
        }
    }

    /// <summary>
    /// Checks if Qdrant vector database is running and accessible.
    /// </summary>
    /// <param name="baseUrl">Qdrant base URL (default: http://localhost:6333)</param>
    /// <returns>True if Qdrant is accessible, false otherwise</returns>
    public static async Task<bool> IsQdrantAvailableAsync(string? baseUrl = null)
    {
        var url = baseUrl ?? QdrantUrl;

        try
        {
            var response = await HttpClient.GetAsync($"{url}/collections");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if Redis cache is running and accessible.
    /// </summary>
    /// <param name="host">Redis host (default: localhost)</param>
    /// <param name="port">Redis port (default: 6379)</param>
    /// <returns>True if Redis is accessible, false otherwise</returns>
    public static async Task<bool> IsRedisAvailableAsync(string? host = null, int? port = null)
    {
        var redisHost = host ?? RedisDefaultHost;
        var redisPort = port ?? RedisDefaultPort;

        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync(redisHost, redisPort);
            return tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Skips the test if API is not available.
    /// </summary>
    /// <param name="baseUrl">API base URL (default: http://localhost:8080)</param>
    /// <exception cref="SkipException">Thrown when API is not available</exception>
    public static async Task SkipIfApiNotAvailableAsync(string? baseUrl = null)
    {
        var url = baseUrl ?? ApiBaseUrl;

        if (!await IsApiAvailableAsync(url))
        {
            Assert.Skip(
                $"❌ API not available at {url}\n" +
                "Prerequisites:\n" +
                "  1. Start API: cd apps/api/src/Api && dotnet run\n" +
                "  2. Ensure services running: docker compose up postgres qdrant redis\n" +
                "  3. Verify API health: curl http://localhost:8080/health"
            );
        }
    }

    /// <summary>
    /// Skips the test if Qdrant is not available.
    /// </summary>
    /// <param name="baseUrl">Qdrant base URL (default: http://localhost:6333)</param>
    /// <exception cref="SkipException">Thrown when Qdrant is not available</exception>
    public static async Task SkipIfQdrantNotAvailableAsync(string? baseUrl = null)
    {
        var url = baseUrl ?? QdrantUrl;

        if (!await IsQdrantAvailableAsync(url))
        {
            Assert.Skip(
                $"❌ Qdrant not available at {url}\n" +
                "Prerequisites:\n" +
                "  1. Start Qdrant: docker compose up -d qdrant\n" +
                "  2. Verify Qdrant: curl http://localhost:6333/collections"
            );
        }
    }

    /// <summary>
    /// Skips the test if Redis is not available.
    /// </summary>
    /// <param name="host">Redis host (default: localhost)</param>
    /// <param name="port">Redis port (default: 6379)</param>
    /// <exception cref="SkipException">Thrown when Redis is not available</exception>
    public static async Task SkipIfRedisNotAvailableAsync(string? host = null, int? port = null)
    {
        var redisHost = host ?? RedisDefaultHost;
        var redisPort = port ?? RedisDefaultPort;

        if (!await IsRedisAvailableAsync(redisHost, redisPort))
        {
            Assert.Skip(
                $"❌ Redis not available at {redisHost}:{redisPort}\n" +
                "Prerequisites:\n" +
                "  1. Start Redis: docker compose up -d redis\n" +
                "  2. Verify Redis: docker exec -it meepleai-redis redis-cli ping"
            );
        }
    }

    /// <summary>
    /// Skips the test if any required E2E infrastructure is unavailable.
    /// Checks API, Qdrant, and Redis.
    /// </summary>
    /// <exception cref="SkipException">Thrown when any required service is not available</exception>
    public static async Task SkipIfE2EInfrastructureNotAvailableAsync()
    {
        var apiAvailable = await IsApiAvailableAsync();
        var qdrantAvailable = await IsQdrantAvailableAsync();
        var redisAvailable = await IsRedisAvailableAsync();

        if (!apiAvailable || !qdrantAvailable || !redisAvailable)
        {
            var missingServices = new List<string>();

            if (!apiAvailable)
                missingServices.Add("API (http://localhost:8080)");

            if (!qdrantAvailable)
                missingServices.Add("Qdrant (http://localhost:6333)");

            if (!redisAvailable)
                missingServices.Add("Redis (localhost:6379)");

            Assert.Skip(
                $"❌ Required E2E infrastructure not available\n" +
                $"Missing services: {string.Join(", ", missingServices)}\n\n" +
                "Full Setup:\n" +
                "  1. Start infrastructure: cd infra && docker compose up -d postgres qdrant redis\n" +
                "  2. Start API: cd apps/api/src/Api && dotnet run\n" +
                "  3. Wait 30 seconds for services to initialize\n" +
                "  4. Run tests: dotnet test --filter \"Category=E2E\"\n\n" +
                "See docs/05-testing/backend/BACKEND_E2E_TESTING.md for complete setup guide."
            );
        }
    }

    /// <summary>
    /// Verifies all E2E prerequisites and returns detailed status.
    /// </summary>
    /// <returns>Tuple with availability status for each service</returns>
    public static async Task<(bool ApiAvailable, bool QdrantAvailable, bool RedisAvailable)>
        CheckAllPrerequisitesAsync()
    {
        var apiTask = IsApiAvailableAsync();
        var qdrantTask = IsQdrantAvailableAsync();
        var redisTask = IsRedisAvailableAsync();

        await Task.WhenAll(apiTask, qdrantTask, redisTask);

        return (
            ApiAvailable: await apiTask,
            QdrantAvailable: await qdrantTask,
            RedisAvailable: await redisTask
        );
    }
}
