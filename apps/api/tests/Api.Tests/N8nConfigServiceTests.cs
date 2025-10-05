using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class N8nConfigServiceTests : IDisposable
{
    private const string TestUserId = "user-1";
    private const string EncryptionKey = "test-encryption-key";

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<N8nConfigService>> _loggerMock;
    private readonly N8nConfigService _service;

    public N8nConfigServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=N8nConfigTests_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _dbContext.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock
            .Setup(c => c[It.IsAny<string>()])
            .Returns<string>(key => key == "N8N_ENCRYPTION_KEY" ? EncryptionKey : null);
        _loggerMock = new Mock<ILogger<N8nConfigService>>();

        _service = new N8nConfigService(
            _dbContext,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateConfigAsync_NormalizesValuesAndEncryptsApiKey()
    {
        var request = new CreateN8nConfigRequest(
            "Primary",
            "https://example.com/",
            "plain-api-key",
            "https://example.com/webhook/");

        var result = await _service.CreateConfigAsync(TestUserId, request, CancellationToken.None);

        Assert.Equal("Primary", result.Name);
        Assert.Equal("https://example.com", result.BaseUrl);
        Assert.Equal("https://example.com/webhook", result.WebhookUrl);
        Assert.True(result.IsActive);

        var entity = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == result.Id);
        Assert.NotEqual(request.ApiKey, entity.ApiKeyEncrypted);
        Assert.False(string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted));
        Assert.Equal("https://example.com", entity.BaseUrl);
        Assert.Equal("https://example.com/webhook", entity.WebhookUrl);
        Assert.True(entity.IsActive);
        Assert.NotNull(Convert.FromBase64String(entity.ApiKeyEncrypted));
    }

    [Fact]
    public async Task CreateConfigAsync_WithDuplicateName_ThrowsInvalidOperation()
    {
        var request = new CreateN8nConfigRequest("Primary", "https://example.com", "plain-api-key", null);
        await _service.CreateConfigAsync(TestUserId, request, CancellationToken.None);

        var duplicate = new CreateN8nConfigRequest("Primary", "https://other.example", "second", null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateConfigAsync(TestUserId, duplicate, CancellationToken.None));

        Assert.Contains("already exists", exception.Message);
        Assert.Equal(1, await _dbContext.N8nConfigs.CountAsync());
    }

    [Fact]
    public async Task UpdateConfigAsync_UpdatesAllowedFieldsAndReencryptsApiKey()
    {
        var created = await _service.CreateConfigAsync(
            TestUserId,
            new CreateN8nConfigRequest("Primary", "https://example.com", "initial-key", "https://webhook"),
            CancellationToken.None);

        var beforeUpdate = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == created.Id);
        var previousCipher = beforeUpdate.ApiKeyEncrypted;

        var updateRequest = new UpdateN8nConfigRequest(
            "Renamed",
            "https://updated.example.com/",
            "updated-key",
            "https://updated-webhook/",
            false);

        var result = await _service.UpdateConfigAsync(created.Id, updateRequest, CancellationToken.None);

        Assert.Equal("Renamed", result.Name);
        Assert.Equal("https://updated.example.com", result.BaseUrl);
        Assert.Equal("https://updated-webhook", result.WebhookUrl);
        Assert.False(result.IsActive);

        var entity = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("Renamed", entity.Name);
        Assert.Equal("https://updated.example.com", entity.BaseUrl);
        Assert.Equal("https://updated-webhook", entity.WebhookUrl);
        Assert.False(entity.IsActive);
        Assert.NotEqual(previousCipher, entity.ApiKeyEncrypted);
        Assert.NotEqual("updated-key", entity.ApiKeyEncrypted);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithDuplicateName_ThrowsInvalidOperation()
    {
        var first = await _service.CreateConfigAsync(
            TestUserId,
            new CreateN8nConfigRequest("Primary", "https://primary.example", "key1", null),
            CancellationToken.None);

        var second = await _service.CreateConfigAsync(
            TestUserId,
            new CreateN8nConfigRequest("Secondary", "https://secondary.example", "key2", null),
            CancellationToken.None);

        var duplicateNameRequest = new UpdateN8nConfigRequest(
            first.Name,
            null,
            null,
            null,
            null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateConfigAsync(second.Id, duplicateNameRequest, CancellationToken.None));

        Assert.Contains("already exists", exception.Message);

        var entity = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == second.Id);
        Assert.Equal("Secondary", entity.Name);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenSuccessful_SendsPlaintextApiKeyAndPersistsResult()
    {
        var config = await _service.CreateConfigAsync(
            TestUserId,
            new CreateN8nConfigRequest("Primary", "https://example.com", "api-key-123", "https://webhook"),
            CancellationToken.None);

        string? receivedApiKey = null;
        var handler = new DelegateHttpMessageHandler(async (request, token) =>
        {
            Assert.Equal("https://example.com/api/v1/workflows", request.RequestUri!.ToString());
            Assert.True(request.Headers.TryGetValues("X-N8N-API-KEY", out var values));
            receivedApiKey = values!.Single();

            await Task.Delay(10, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler, disposeHandler: false));

        var result = await _service.TestConnectionAsync(config.Id, CancellationToken.None);

        Assert.Equal("api-key-123", receivedApiKey);
        Assert.True(result.Success);
        Assert.NotNull(result.LatencyMs);
        Assert.True(result.LatencyMs!.Value >= 0);
        Assert.Contains("Connection successful", result.Message);

        var entity = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == config.Id);
        Assert.NotNull(entity.LastTestedAt);
        Assert.Contains("Connection successful", entity.LastTestResult);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenResponseFails_PersistsFailureDetails()
    {
        var config = await _service.CreateConfigAsync(
            TestUserId,
            new CreateN8nConfigRequest("Primary", "https://example.com", "api-key-123", null),
            CancellationToken.None);

        var handler = new DelegateHttpMessageHandler((request, token) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler, disposeHandler: false));

        var result = await _service.TestConnectionAsync(config.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.LatencyMs);
        Assert.Contains("Connection failed", result.Message);

        var entity = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == config.Id);
        Assert.NotNull(entity.LastTestedAt);
        Assert.Contains("Connection failed", entity.LastTestResult);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenExceptionThrown_SavesFailureAndNullLatency()
    {
        var config = await _service.CreateConfigAsync(
            TestUserId,
            new CreateN8nConfigRequest("Primary", "https://example.com", "api-key-123", null),
            CancellationToken.None);

        var handler = new DelegateHttpMessageHandler((request, token) =>
        {
            throw new HttpRequestException("boom");
        });

        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler, disposeHandler: false));

        var result = await _service.TestConnectionAsync(config.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.LatencyMs);
        Assert.Contains("Connection failed", result.Message);
        Assert.Contains("boom", result.Message, StringComparison.OrdinalIgnoreCase);

        var entity = await _dbContext.N8nConfigs.SingleAsync(c => c.Id == config.Id);
        Assert.NotNull(entity.LastTestedAt);
        Assert.Contains("boom", entity.LastTestResult, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
