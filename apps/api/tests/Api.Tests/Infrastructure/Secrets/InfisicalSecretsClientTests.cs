using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure.Secrets;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Infrastructure.Secrets;

/// <summary>
/// Unit tests for InfisicalSecretsClient (Issue #936 POC).
/// Tests authentication, secret fetching, and version history capabilities.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class InfisicalSecretsClientTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<InfisicalSecretsClient>> _mockLogger;
    private readonly IOptions<InfisicalOptions> _options;
    private readonly HttpClient _httpClient;

    public InfisicalSecretsClientTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<InfisicalSecretsClient>>();

        _options = Options.Create(new InfisicalOptions
        {
            HostUrl = "http://localhost:8081",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            ProjectId = "test-project-id"
        });

        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8081")
        };

        _mockHttpClientFactory
            .Setup(f => f.CreateClient("Infisical"))
            .Returns(_httpClient);
    }

    [Fact]
    public async Task GetSecretAsync_WithValidSecret_ShouldReturnSecretValue()
    {
        // Arrange
        SetupAuthResponse();
        SetupGetSecretResponse("DATABASE_PASSWORD", "super-secret-password-123", version: 2);

        var client = CreateClient();

        // Act
        var secretValue = await client.GetSecretAsync("DATABASE_PASSWORD", "dev", "/");

        // Assert
        secretValue.Should().Be("super-secret-password-123");
    }

    [Fact]
    public async Task GetSecretAsync_WithInvalidSecret_ShouldThrowHttpRequestException()
    {
        // Arrange
        SetupAuthResponse();
        SetupGetSecretNotFound();

        var client = CreateClient();

        // Act
        var act = () => client.GetSecretAsync("NON_EXISTENT_SECRET", "dev", "/");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetSecretVersionsAsync_WithVersionHistory_ShouldReturnAllVersions()
    {
        // Arrange
        SetupAuthResponse();
        SetupGetVersionsResponse("DATABASE_PASSWORD", new[]
        {
            ("version3-password", 3, DateTime.UtcNow),
            ("version2-password", 2, DateTime.UtcNow.AddDays(-15)),
            ("version1-password", 1, DateTime.UtcNow.AddDays(-30))
        });

        var client = CreateClient();

        // Act
        var versions = await client.GetSecretVersionsAsync("DATABASE_PASSWORD", "dev", "/");

        // Assert
        versions.Should().HaveCount(3);
        versions[0].Version.Should().Be(3);
        versions[0].Value.Should().Be("version3-password");
        versions[1].Version.Should().Be(2);
        versions[2].Version.Should().Be(1);
    }

    [Fact]
    public async Task HealthCheckAsync_WithValidConnection_ShouldReturnTrue()
    {
        // Arrange
        SetupAuthResponse();
        SetupWorkspaceInfoResponse();

        var client = CreateClient();

        // Act
        var isHealthy = await client.HealthCheckAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheckAsync_WithAuthFailure_ShouldReturnFalse()
    {
        // Arrange
        SetupAuthFailure();

        var client = CreateClient();

        // Act
        var isHealthy = await client.HealthCheckAsync();

        // Assert
        isHealthy.Should().BeFalse();
    }

    // Helper methods for HTTP mock setup
    private void SetupAuthResponse()
    {
        var authResponse = new
        {
            accessToken = "mock-access-token-abc123",
            expiresIn = 3600,
            accessTokenMaxTTL = 7200,
            tokenType = "Bearer"
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.PathAndQuery.Contains("/api/v1/auth/universal-auth/login")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(authResponse)
            });
    }

    private void SetupGetSecretResponse(string secretName, string secretValue, int version)
    {
        var secretResponse = new
        {
            secret = new
            {
                _id = "mock-secret-id",
                version,
                workspace = "test-workspace",
                environment = "dev",
                secretKey = secretName,
                secretValue,
                createdAt = DateTime.UtcNow.AddDays(-10),
                updatedAt = DateTime.UtcNow
            }
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.PathAndQuery.Contains($"/api/v3/secrets/raw/{secretName}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(secretResponse)
            });
    }

    private void SetupGetSecretNotFound()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.PathAndQuery.Contains("/api/v3/secrets/raw/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });
    }

    private void SetupGetVersionsResponse(
        string secretName,
        (string Value, int Version, DateTime CreatedAt)[] versions)
    {
        var versionResponse = new
        {
            secretVersions = versions.Select(v => new
            {
                version = v.Version,
                secretValue = v.Value,
                createdAt = v.CreatedAt,
                createdBy = "test-user"
            }).ToArray()
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.PathAndQuery.Contains("/api/v3/secrets/") &&
                    req.RequestUri!.PathAndQuery.Contains("/secret-versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(versionResponse)
            });
    }

    private void SetupWorkspaceInfoResponse()
    {
        var workspaceResponse = new
        {
            workspace = new
            {
                _id = "test-project-id",
                name = "meepleai-poc",
                organization = "test-org"
            }
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.PathAndQuery.Contains("/api/v1/workspace/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(workspaceResponse)
            });
    }

    private void SetupAuthFailure()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });
    }

    private InfisicalSecretsClient CreateClient()
    {
        return new InfisicalSecretsClient(
            _mockHttpClientFactory.Object,
            _options,
            _mockLogger.Object);
    }
}
