using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class AdminEndpointsIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebApplicationFactoryFixture _factory;

    public AdminEndpointsIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAdminRequests_AdminReceivesFilteredLogsWithMetadata()
    {
        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-requests-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        var editorEmail = $"editor-requests-{Guid.NewGuid():N}@example.com";
        using var editorClient = CreateClientWithoutCookies();
        await RegisterAndAuthenticateAsync(editorClient, editorEmail, "Editor");
        var editorUserId = await GetUserIdByEmailAsync(editorEmail);

        var seedContext = await SeedDashboardDataAsync(adminUserId, editorUserId);

        var requestUri = $"/admin/requests?limit=10&offset=0&userId={adminUserId}&gameId=game-1" +
                         $"&startDate={Uri.EscapeDataString(seedContext.StartDate.ToString("O"))}" +
                         $"&endDate={Uri.EscapeDataString(seedContext.EndDate.ToString("O"))}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("requests", out var requestsElement));
        Assert.Equal(JsonValueKind.Array, requestsElement.ValueKind);
        Assert.Equal(2, requestsElement.GetArrayLength());

        var endpoints = requestsElement
            .EnumerateArray()
            .Select(element => element.GetProperty("endpoint").GetString())
            .ToList();

        Assert.Contains("qa", endpoints);
        Assert.Contains("setup", endpoints);

        var qaLog = requestsElement
            .EnumerateArray()
            .Single(element => element.GetProperty("endpoint").GetString() == "qa");

        Assert.Equal("integration-test/1.0", qaLog.GetProperty("userAgent").GetString());
        Assert.Equal("127.0.0.1", qaLog.GetProperty("ipAddress").GetString());
        Assert.Equal("gpt-4", qaLog.GetProperty("model").GetString());
        Assert.Equal("stop", qaLog.GetProperty("finishReason").GetString());
        Assert.Equal("Success", qaLog.GetProperty("status").GetString());
        Assert.Equal(50, qaLog.GetProperty("tokenCount").GetInt32());
        Assert.Equal(30, qaLog.GetProperty("promptTokens").GetInt32());
        Assert.Equal(20, qaLog.GetProperty("completionTokens").GetInt32());

        var setupLog = requestsElement
            .EnumerateArray()
            .Single(element => element.GetProperty("endpoint").GetString() == "setup");

        Assert.Equal("Error", setupLog.GetProperty("status").GetString());
        Assert.Equal("timeout", setupLog.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task GetAdminStats_AdminReceivesAggregatedValues()
    {
        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-stats-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        var userEmail = $"user-stats-{Guid.NewGuid():N}@example.com";
        using var userClient = CreateClientWithoutCookies();
        await RegisterAndAuthenticateAsync(userClient, userEmail, "User");
        var userId = await GetUserIdByEmailAsync(userEmail);

        var seedContext = await SeedDashboardDataAsync(adminUserId, userId);

        var requestUri = $"/admin/stats?userId={adminUserId}&gameId=game-1" +
                         $"&startDate={Uri.EscapeDataString(seedContext.StartDate.ToString("O"))}" +
                         $"&endDate={Uri.EscapeDataString(seedContext.EndDate.ToString("O"))}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("totalRequests").GetInt32());
        Assert.Equal(100, root.GetProperty("avgLatencyMs").GetDouble(), 2);
        Assert.Equal(80, root.GetProperty("totalTokens").GetInt32());
        Assert.Equal(0.5, root.GetProperty("successRate").GetDouble(), 3);

        var endpointCounts = root.GetProperty("endpointCounts");
        Assert.Equal(1, endpointCounts.GetProperty("qa").GetInt32());
        Assert.Equal(1, endpointCounts.GetProperty("setup").GetInt32());

        var feedbackCounts = root.GetProperty("feedbackCounts");
        Assert.Equal(1, feedbackCounts.GetProperty("helpful").GetInt32());
        Assert.Equal(1, feedbackCounts.GetProperty("not-helpful").GetInt32());
        Assert.Equal(2, root.GetProperty("totalFeedback").GetInt32());

        var feedbackByEndpoint = root.GetProperty("feedbackByEndpoint");
        Assert.Equal(1, feedbackByEndpoint.GetProperty("qa").GetProperty("helpful").GetInt32());
        Assert.Equal(1, feedbackByEndpoint.GetProperty("setup").GetProperty("not-helpful").GetInt32());
    }

    public static IEnumerable<object[]> NonAdminRoles => new[]
    {
        new object[] { "Editor" },
        new object[] { "User" }
    };

    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task GetAdminRequests_ReturnsForbiddenForNonAdminRoles(string role)
    {
        using var nonAdminClient = _factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-requests-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/requests");
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminRequests_ReturnsUnauthorizedForAnonymousUser()
    {
        using var client = _factory.CreateHttpsClient();
        var response = await client.GetAsync("/admin/requests");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task GetAdminStats_ReturnsForbiddenForNonAdminRoles(string role)
    {
        using var nonAdminClient = _factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-stats-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/stats");
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminStats_ReturnsUnauthorizedForAnonymousUser()
    {
        using var client = _factory.CreateHttpsClient();
        var response = await client.GetAsync("/admin/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task PostAdminN8n_ReturnsForbiddenForNonAdminRoles(string role)
    {
        using var nonAdminClient = _factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-n8n-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/n8n")
        {
            Content = JsonContent.Create(new CreateN8nConfigRequest("Forbidden", "https://n8n.invalid", "key", null))
        };
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostAdminN8n_CreatesConfigAndPersistsEncryptedKey()
    {
        await ClearN8nConfigsAsync();

        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-create-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/n8n")
        {
            Content = JsonContent.Create(new CreateN8nConfigRequest(
                "Primary Workflow",
                "https://n8n.local/",
                "test-api-key",
                "https://n8n.local/webhook"))
        };
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("Primary Workflow", dto!.Name);
        Assert.Equal("https://n8n.local", dto.BaseUrl);
        Assert.Equal("https://n8n.local/webhook", dto.WebhookUrl);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await db.N8nConfigs.SingleAsync(c => c.Id == dto.Id);
        Assert.Equal(adminUserId, entity.CreatedByUserId);
        Assert.Equal("https://n8n.local", entity.BaseUrl);
        Assert.False(string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted));
        Assert.NotEqual("test-api-key", entity.ApiKeyEncrypted);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/n8n");
        AddCookies(listRequest, cookies);
        var listResponse = await adminClient.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var configs = listDocument.RootElement.GetProperty("configs");
        Assert.Contains(configs.EnumerateArray(), element => element.GetProperty("id").GetString() == dto.Id);
    }

    [Fact]
    public async Task GetAdminN8nById_ReturnsPersistedConfig()
    {
        await ClearN8nConfigsAsync();

        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-get-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        var config = await CreateN8nConfigAsync(adminUserId, "Existing Workflow");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/admin/n8n/{config.Id}");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(config.Id, dto!.Id);
        Assert.Equal(config.Name, dto.Name);
    }

    [Fact]
    public async Task PutAdminN8n_UpdatesConfigAndRotatesApiKey()
    {
        await ClearN8nConfigsAsync();

        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-update-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        var existing = await CreateN8nConfigAsync(adminUserId, "Workflow To Update");

        string originalEncryptedKey;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            originalEncryptedKey = (await db.N8nConfigs.SingleAsync(c => c.Id == existing.Id)).ApiKeyEncrypted;
        }

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/admin/n8n/{existing.Id}")
        {
            Content = JsonContent.Create(new UpdateN8nConfigRequest(
                "Updated Workflow",
                "https://n8n.updated/",
                "rotated-api-key",
                "https://n8n.updated/webhook",
                false))
        };
        AddCookies(updateRequest, cookies);

        var response = await adminClient.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("Updated Workflow", dto!.Name);
        Assert.Equal("https://n8n.updated", dto.BaseUrl);
        Assert.Equal("https://n8n.updated/webhook", dto.WebhookUrl);
        Assert.False(dto.IsActive);

        await using (var verifyScope = _factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.N8nConfigs.SingleAsync(c => c.Id == existing.Id);
            Assert.Equal("Updated Workflow", entity.Name);
            Assert.Equal("https://n8n.updated", entity.BaseUrl);
            Assert.Equal("https://n8n.updated/webhook", entity.WebhookUrl);
            Assert.False(entity.IsActive);
            Assert.NotEqual(originalEncryptedKey, entity.ApiKeyEncrypted);
            Assert.True(entity.UpdatedAt > entity.CreatedAt);
        }
    }

    [Fact]
    public async Task DeleteAdminN8n_ReturnsNotFoundForMissingConfig()
    {
        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-delete-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        var request = new HttpRequestMessage(HttpMethod.Delete, "/admin/n8n/non-existent-config");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Configuration not found", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostAdminN8nTestConnection_UpdatesTestMetadata()
    {
        await ClearN8nConfigsAsync();

        using var adminClient = _factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-test-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        var config = await CreateN8nConfigAsync(adminUserId, "Workflow To Test");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/n8n/{config.Id}/test");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<N8nTestResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.StartsWith("Connection successful", result.Message, StringComparison.Ordinal);
        Assert.True(result.LatencyMs.HasValue);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.N8nConfigs.SingleAsync(c => c.Id == config.Id);
            Assert.NotNull(entity.LastTestedAt);
            Assert.False(string.IsNullOrWhiteSpace(entity.LastTestResult));
            Assert.StartsWith("Connection successful", entity.LastTestResult, StringComparison.Ordinal);
        }
    }

    private HttpClient CreateClientWithoutCookies()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    private static void AddCookies(HttpRequestMessage request, IEnumerable<string> cookies)
    {
        var cookieList = cookies
            .Where(cookie => !string.IsNullOrWhiteSpace(cookie))
            .ToList();

        if (cookieList.Count == 0)
        {
            return;
        }

        request.Headers.Remove("Cookie");
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookieList));
    }

    private static List<string> ExtractCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return new List<string>();
        }

        return values
            .Select(value => value.Split(';')[0])
            .ToList();
    }

    private async Task<List<string>> RegisterAndAuthenticateAsync(HttpClient client, string email, string role)
    {
        var payload = new RegisterPayload(email, "Password123!", "Integration Tester", null);
        var response = await client.PostAsJsonAsync("/auth/register", payload);
        response.EnsureSuccessStatusCode();
        if (!string.Equals(role, UserRole.User.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var parsedRole = Enum.Parse<UserRole>(role, true);
            await PromoteUserAsync(email, parsedRole);
        }

        return ExtractCookies(response);
    }

    private async Task<string> GetUserIdByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return user.Id;
    }

    private async Task PromoteUserAsync(string email, UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    private async Task<DashboardSeedContext> SeedDashboardDataAsync(string adminUserId, string otherUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        db.AiRequestLogs.RemoveRange(db.AiRequestLogs);
        db.AgentFeedbacks.RemoveRange(db.AgentFeedbacks);
        await db.SaveChangesAsync();

        var baseTime = DateTime.UtcNow;

        var adminSuccess = new AiRequestLogEntity
        {
            UserId = adminUserId,
            GameId = "game-1",
            Endpoint = "qa",
            Query = "How to score points?",
            ResponseSnippet = "Prioritize engine building.",
            LatencyMs = 120,
            TokenCount = 50,
            PromptTokens = 30,
            CompletionTokens = 20,
            Confidence = 0.9,
            Status = "Success",
            IpAddress = "127.0.0.1",
            UserAgent = "integration-test/1.0",
            Model = "gpt-4",
            FinishReason = "stop",
            CreatedAt = baseTime.AddMinutes(-30)
        };

        var adminError = new AiRequestLogEntity
        {
            UserId = adminUserId,
            GameId = "game-1",
            Endpoint = "setup",
            Query = "How do I set up?",
            ResponseSnippet = "Unable to answer.",
            LatencyMs = 80,
            TokenCount = 30,
            PromptTokens = 18,
            CompletionTokens = 12,
            Confidence = 0.5,
            Status = "Error",
            ErrorMessage = "timeout",
            IpAddress = "127.0.0.1",
            UserAgent = "integration-test/2.0",
            Model = "gpt-4",
            FinishReason = "length",
            CreatedAt = baseTime.AddMinutes(-20)
        };

        var otherLog = new AiRequestLogEntity
        {
            UserId = otherUserId,
            GameId = "game-2",
            Endpoint = "qa",
            Query = "Unrelated question",
            ResponseSnippet = "Different game response.",
            LatencyMs = 60,
            TokenCount = 20,
            PromptTokens = 10,
            CompletionTokens = 10,
            Confidence = 0.8,
            Status = "Success",
            IpAddress = "192.168.1.10",
            UserAgent = "integration-test/3.0",
            Model = "gpt-3.5",
            FinishReason = "stop",
            CreatedAt = baseTime.AddMinutes(-10)
        };

        db.AiRequestLogs.AddRange(adminSuccess, adminError, otherLog);

        var helpfulFeedback = new AgentFeedbackEntity
        {
            MessageId = "msg-helpful",
            Endpoint = "qa",
            GameId = "game-1",
            UserId = adminUserId,
            Outcome = "helpful",
            CreatedAt = baseTime.AddMinutes(-25),
            UpdatedAt = baseTime.AddMinutes(-25)
        };

        var notHelpfulFeedback = new AgentFeedbackEntity
        {
            MessageId = "msg-not-helpful",
            Endpoint = "setup",
            GameId = "game-1",
            UserId = adminUserId,
            Outcome = "not-helpful",
            CreatedAt = baseTime.AddMinutes(-15),
            UpdatedAt = baseTime.AddMinutes(-15)
        };

        var otherFeedback = new AgentFeedbackEntity
        {
            MessageId = "msg-other",
            Endpoint = "qa",
            GameId = "game-2",
            UserId = otherUserId,
            Outcome = "helpful",
            CreatedAt = baseTime.AddMinutes(-5),
            UpdatedAt = baseTime.AddMinutes(-5)
        };

        db.AgentFeedbacks.AddRange(helpfulFeedback, notHelpfulFeedback, otherFeedback);

        await db.SaveChangesAsync();

        return new DashboardSeedContext(
            baseTime.AddMinutes(-35),
            baseTime.AddMinutes(-15));
    }

    private async Task ClearN8nConfigsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.N8nConfigs.RemoveRange(db.N8nConfigs);
        await db.SaveChangesAsync();
    }

    private async Task<N8nConfigDto> CreateN8nConfigAsync(string userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<N8nConfigService>();
        return await service.CreateConfigAsync(
            userId,
            new CreateN8nConfigRequest(
                name,
                "https://n8n.seed/",
                "seed-api-key",
                "https://n8n.seed/webhook"),
            default);
    }

    private sealed record DashboardSeedContext(DateTime StartDate, DateTime EndDate);
}
