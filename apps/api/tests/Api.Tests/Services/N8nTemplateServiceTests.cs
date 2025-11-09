using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

public class N8nTemplateServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly MeepleAiDbContext _db;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<N8nTemplateService>> _mockLogger;
    private readonly N8nTemplateService _service;
    private readonly string _tempTemplatesPath;

    public N8nTemplateServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new MeepleAiDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<N8nTemplateService>>();

        // Set up encryption key configuration
        _configurationMock.Setup(c => c["N8N_ENCRYPTION_KEY"])
            .Returns("test-encryption-key-for-unit-tests-32bytes");

        _service = new N8nTemplateService(
            _db,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _mockLogger.Object
        );

        // Create temporary templates directory for testing
        _tempTemplatesPath = Path.Combine(Path.GetTempPath(), $"n8n_templates_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempTemplatesPath);

        // Use reflection to set the templates path
        var field = typeof(N8nTemplateService).GetField("_templatesPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(_service, _tempTemplatesPath);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();

        // Clean up temporary templates directory
        if (Directory.Exists(_tempTemplatesPath))
        {
            Directory.Delete(_tempTemplatesPath, true);
        }
    }

    #region GetTemplatesAsync Tests

    [Fact]
    public async Task GetTemplatesAsync_ReturnsEmptyList_WhenNoTemplatesExist()
    {
        // Act
        var result = await _service.GetTemplatesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsAllTemplates_WhenNoCategoryFilter()
    {
        // Arrange
        CreateTestTemplate("template1", "integration");
        CreateTestTemplate("template2", "automation");

        // Act
        var result = await _service.GetTemplatesAsync();

        // Assert
        result.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetTemplatesAsync_FiltersByCategory_WhenCategorySpecified()
    {
        // Arrange
        CreateTestTemplate("template1", "integration");
        CreateTestTemplate("template2", "automation");
        CreateTestTemplate("template3", "integration");

        // Act
        var result = await _service.GetTemplatesAsync("integration");

        // Assert
        result.Count.Should().Be(2);
        result.Should().OnlyContain(t => t.Category == "integration");
    }

    [Fact]
    public async Task GetTemplatesAsync_SortsTemplates_ByCategoryThenName()
    {
        // Arrange
        CreateTestTemplate("zebra", "integration", "Zebra Template");
        CreateTestTemplate("alpha", "automation", "Alpha Template");
        CreateTestTemplate("beta", "integration", "Beta Template");

        // Act
        var result = await _service.GetTemplatesAsync();

        // Assert
        result.Count.Should().Be(3);
        result[0].Name.Should().Be("Alpha Template");
        result[1].Name.Should().Be("Beta Template");
        result[2].Name.Should().Be("Zebra Template");
    }

    [Fact]
    public async Task GetTemplatesAsync_SkipsInvalidTemplates_AndLogsError()
    {
        // Arrange
        CreateTestTemplate("valid", "integration");
        File.WriteAllText(
            Path.Combine(_tempTemplatesPath, "invalid.json"),
            "{ invalid json }");

        // Act
        var result = await _service.GetTemplatesAsync();

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be("valid");
    }

    #endregion

    #region GetTemplateAsync Tests

    [Fact]
    public async Task GetTemplateAsync_ReturnsNull_WhenTemplateNotFound()
    {
        // Act
        var result = await _service.GetTemplateAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTemplateAsync_ReturnsTemplate_WhenTemplateExists()
    {
        // Arrange
        CreateTestTemplate("test-template", "integration");

        // Act
        var result = await _service.GetTemplateAsync("test-template");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeEquivalentTo("test-template");
        result.Category.Should().BeEquivalentTo("integration");
        result.Workflow.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTemplateAsync_IncludesWorkflowDetails()
    {
        // Arrange
        CreateTestTemplate("test", "integration");

        // Act
        var result = await _service.GetTemplateAsync("test");

        // Assert
        result.Should().NotBeNull();
        result.Workflow.Should().NotBeNull();
    }

    #endregion

    #region ImportTemplateAsync Tests

    [Fact]
    public async Task ImportTemplateAsync_ThrowsException_WhenTemplateNotFound()
    {
        // Arrange
        var parameters = new Dictionary<string, string>();

        // Act & Assert
        var act = async () => await _service.ImportTemplateAsync("nonexistent", parameters, "user123");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ImportTemplateAsync_ThrowsException_WhenMissingRequiredParameters()
    {
        // Arrange
        CreateTestTemplateWithParameters("test", new[]
        {
            new { name = "param1", required = true },
            new { name = "param2", required = false }
        });

        var parameters = new Dictionary<string, string>
        {
            { "param2", "value2" } // Missing required param1
        };

        // Act & Assert
        var act = async () => await _service.ImportTemplateAsync("test", parameters, "user123");
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain("param1");
    }

    [Fact]
    public async Task ImportTemplateAsync_ThrowsException_WhenNoActiveN8nConfig()
    {
        // Arrange
        CreateTestTemplate("test", "integration");
        var parameters = new Dictionary<string, string>();

        // Act & Assert
        var act = async () => await _service.ImportTemplateAsync("test", parameters, "user123");
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.Which.Message.Should().Contain("No active n8n configuration");
    }

    [Fact]
    public async Task ImportTemplateAsync_CallsN8nApi_WithCorrectPayload()
    {
        // Arrange
        await SeedActiveN8nConfig();
        CreateTestTemplate("test", "integration");

        var parameters = new Dictionary<string, string>();

        // Mock HTTP client
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new { id = "workflow-123" }),
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _service.ImportTemplateAsync("test", parameters, "user123");

        // Assert
        result.Should().NotBeNull();
        result.WorkflowId.Should().BeEquivalentTo("workflow-123");
        result.Message.ToLower().Should().Contain("imported successfully");

        // Verify HTTP request was made
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("/api/v1/workflows") &&
                req.Headers.Contains("X-N8N-API-KEY")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ImportTemplateAsync_SubstitutesParameters_InWorkflowJson()
    {
        // Arrange
        await SeedActiveN8nConfig();
        CreateTestTemplateWithPlaceholders("test-params", new Dictionary<string, string>
        {
            { "apiUrl", "http://localhost" },
            { "interval", "*/5 * * * *" }
        });

        var parameters = new Dictionary<string, string>
        {
            { "apiUrl", "https://api.example.com" },
            { "interval", "0 * * * *" }
        };

        string? capturedRequestBody = null;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                // CODE-01: Read content before HttpClient disposes it
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new { id = "workflow-456" }),
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        await _service.ImportTemplateAsync("test-params", parameters, "user123");

        // Assert
        capturedRequestBody.Should().NotBeNull();
        var requestBody = capturedRequestBody!;

        requestBody.Should().Contain("https://api.example.com");
        requestBody.Should().Contain("0 * * * *");
        requestBody.Should().NotContain("{{apiUrl}}");
        requestBody.Should().NotContain("{{interval}}");
    }

    [Fact]
    public async Task ImportTemplateAsync_ThrowsException_WhenN8nApiReturnsError()
    {
        // Arrange
        await SeedActiveN8nConfig();
        CreateTestTemplate("test", "integration");

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid workflow")
            });

        using var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act & Assert
        var act = async () => await _service.ImportTemplateAsync("test", new Dictionary<string, string>(), "user123");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region ValidateTemplate Tests

    [Fact]
    public void ValidateTemplate_ReturnsFalse_WhenJsonIsInvalid()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var result = _service.ValidateTemplate(invalidJson);

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().Contain(e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void ValidateTemplate_ReturnsFalse_WhenMissingRequiredFields()
    {
        // Arrange
        var incompleteTemplate = JsonSerializer.Serialize(new
        {
            id = "test",
            // Missing: name, version, description, category, workflow, parameters
        });

        // Act
        var result = _service.ValidateTemplate(incompleteTemplate);

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().Contain(e => e.Contains("name"));
        result.Errors.Should().Contain(e => e.Contains("version"));
    }

    [Fact]
    public void ValidateTemplate_ReturnsFalse_WhenWorkflowMissingNodes()
    {
        // Arrange
        var template = JsonSerializer.Serialize(new
        {
            id = "test",
            name = "Test",
            version = "1.0.0",
            description = "Test",
            category = "integration",
            parameters = new object[] { },
            workflow = new
            {
                // Missing nodes
                connections = new { }
            }
        });

        // Act
        var result = _service.ValidateTemplate(template);

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().Contain(e => e.Contains("nodes"));
    }

    [Fact]
    public void ValidateTemplate_ReturnsTrue_WhenTemplateIsValid()
    {
        // Arrange
        var validTemplate = CreateValidTemplateJson("test", "integration");

        // Act
        var result = _service.ValidateTemplate(validTemplate);

        // Assert
        result.Valid.Should().BeTrue();
        result.Errors.Should().BeNull();
    }

    [Fact]
    public void ValidateTemplate_ReturnsFalse_WhenParameterMissingName()
    {
        // Arrange
        var template = JsonSerializer.Serialize(new
        {
            id = "test",
            name = "Test",
            version = "1.0.0",
            description = "Test",
            category = "integration",
            parameters = new[]
            {
                new
                {
                    // Missing name
                    type = "string",
                    label = "Label",
                    description = "Desc",
                    required = true
                }
            },
            workflow = new
            {
                nodes = new[] { new { id = "1", type = "test" } },
                connections = new { }
            }
        });

        // Act
        var result = _service.ValidateTemplate(template);

        // Assert
        result.Valid.Should().BeFalse();
        result.Errors!.Should().Contain(e => e.Contains("name is required"));
    }

    #endregion

    #region Helper Methods

    private void CreateTestTemplate(string id, string category, string? name = null)
    {
        var template = new
        {
            id,
            name = name ?? $"{id} Template",
            version = "1.0.0",
            description = "Test template",
            category,
            author = "Test",
            tags = new[] { "test" },
            icon = "📋",
            screenshot = (string?)null,
            documentation = (string?)null,
            parameters = new object[] { },
            workflow = new
            {
                nodes = new[]
                {
                    new { id = "node1", type = "n8n-nodes-base.webhook", position = new[] { 100, 100 } }
                },
                connections = new { },
                settings = new { executionOrder = "v1" },
                staticData = (object?)null
            }
        };

        var json = JsonSerializer.Serialize(template);
        File.WriteAllText(Path.Combine(_tempTemplatesPath, $"{id}.json"), json);
    }

    private void CreateTestTemplateWithParameters(string id, IEnumerable<dynamic> parameters)
    {
        var template = new
        {
            id,
            name = $"{id} Template",
            version = "1.0.0",
            description = "Test template",
            category = "integration",
            author = "Test",
            tags = new[] { "test" },
            icon = "📋",
            screenshot = (string?)null,
            documentation = (string?)null,
            parameters = parameters.Select(p => new
            {
                name = p.name,
                type = "string",
                label = p.name,
                description = "Test param",
                required = p.required,
                @default = (string?)null,
                options = (string[]?)null,
                sensitive = false
            }).ToArray(),
            workflow = new
            {
                nodes = new[] { new { id = "node1", type = "test" } },
                connections = new { }
            }
        };

        var json = JsonSerializer.Serialize(template);
        File.WriteAllText(Path.Combine(_tempTemplatesPath, $"{id}.json"), json);
    }

    private void CreateTestTemplateWithPlaceholders(string id, Dictionary<string, string> placeholders)
    {
        var template = new
        {
            id,
            name = $"{id} Template",
            version = "1.0.0",
            description = "Test template with placeholders",
            category = "integration",
            author = "Test",
            tags = new[] { "test" },
            icon = "📋",
            parameters = placeholders.Keys.Select(key => new
            {
                name = key,
                type = "string",
                label = key,
                description = $"{key} parameter",
                required = true,
                @default = (string?)null,
                options = (string[]?)null,
                sensitive = false
            }).ToArray(),
            workflow = new
            {
                nodes = new[]
                {
                    new
                    {
                        id = "node1",
                        type = "n8n-nodes-base.httpRequest",
                        parameters = new
                        {
                            url = $"{{{{{placeholders.Keys.First()}}}}}",
                            schedule = placeholders.Count > 1 ? $"{{{{{placeholders.Keys.Last()}}}}}" : null
                        }
                    }
                },
                connections = new { }
            }
        };

        var json = JsonSerializer.Serialize(template);
        File.WriteAllText(Path.Combine(_tempTemplatesPath, $"{id}.json"), json);
    }

    private string CreateValidTemplateJson(string id, string category)
    {
        var template = new
        {
            id,
            name = "Valid Template",
            version = "1.0.0",
            description = "A valid template",
            category,
            author = "Test",
            tags = new[] { "test" },
            icon = "📋",
            parameters = new object[] { },
            workflow = new
            {
                nodes = new[] { new { id = "1", type = "webhook" } },
                connections = new { }
            }
        };

        return JsonSerializer.Serialize(template);
    }

    private async Task SeedActiveN8nConfig()
    {
        // Create test user first (required for foreign key constraint)
        var user = new UserEntity
        {
            Id = "admin",
            Email = "admin@test.com",
            DisplayName = "Test Admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);

        var config = new N8nConfigEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Config",
            BaseUrl = "http://localhost:5678",
            ApiKeyEncrypted = EncryptTestApiKey("test-api-key"),
            WebhookUrl = null,
            IsActive = true,
            CreatedByUserId = "admin",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.N8nConfigs.Add(config);
        await _db.SaveChangesAsync();
    }

    private string EncryptTestApiKey(string apiKey)
    {
        // Simple encryption for testing - matches N8nConfigService logic
        using var aes = System.Security.Cryptography.Aes.Create();
        var key = GetTestEncryptionKey();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(apiKey);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    private byte[] GetTestEncryptionKey()
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes("test-encryption-key-for-unit-tests-32bytes"));
    }

    #endregion
}
