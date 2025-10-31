using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

public class N8nTemplateEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly ITestOutputHelper _output;

    private readonly WebApplicationFactory<Program> _factory;

    public N8nTemplateEndpointsTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory;
    }

    [Fact]
    public async Task GetTemplates_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplates_ReturnsTemplates_WhenAuthenticated()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var templates = JsonSerializer.Deserialize<List<WorkflowTemplateDto>>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(templates);
        // Should have templates from infra/n8n/templates/
        Assert.NotEmpty(templates);
    }

    [Fact]
    public async Task GetTemplates_FiltersByCategory_WhenCategoryProvided()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates?category=integration");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var templates = JsonSerializer.Deserialize<List<WorkflowTemplateDto>>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(templates);
        Assert.All(templates!, t => Assert.Equal("integration", t.Category));
    }

    [Fact]
    public async Task GetTemplate_ReturnsTemplate_WhenTemplateExists()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();
        var templateId = "bgg-game-sync"; // Known template from seed data

        // Act
        var response = await client.GetAsync($"/api/v1/n8n/templates/{templateId}");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var template = JsonSerializer.Deserialize<WorkflowTemplateDetailDto>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(template);
        Assert.Equal(templateId, template!.Id);
        Assert.NotNull(template.Workflow);
    }

    [Fact]
    public async Task GetTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates/nonexistent-template");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportTemplate_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ImportTemplateRequest(new Dictionary<string, string>());
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/n8n/templates/test/import", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportTemplate_ReturnsBadRequest_WhenMissingRequiredParameters()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // bgg-game-sync template requires bggUsername, syncInterval, apiBaseUrl
        var request = new ImportTemplateRequest(new Dictionary<string, string>
        {
            { "syncInterval", "0 2 * * *" }
            // Missing required: bggUsername, apiBaseUrl
        });

        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/n8n/templates/bgg-game-sync/import", httpContent);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("required", responseContent.ToLower());
    }

    [Fact]
    public async Task ImportTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();
        var request = new ImportTemplateRequest(new Dictionary<string, string>());
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/n8n/templates/nonexistent/import", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("not found", responseContent.ToLower());
    }

    [Fact]
    public async Task ValidateTemplate_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ValidateTemplateRequest("{}");
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/n8n/templates/validate", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidateTemplate_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange
        var client = await CreateAuthenticatedClient("user@meepleai.dev", "User");
        var request = new ValidateTemplateRequest("{}");
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/n8n/templates/validate", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidateTemplate_ReturnsValidationResult_ForAdmin()
    {
        // Arrange
        var client = await CreateAuthenticatedClient("admin@meepleai.dev", "Admin");
        var validTemplate = JsonSerializer.Serialize(new
        {
            id = "test",
            name = "Test",
            version = "1.0.0",
            description = "Test",
            category = "integration",
            parameters = new object[] { },
            workflow = new
            {
                nodes = new[] { new { id = "1" } },
                connections = new { }
            }
        });

        var request = new ValidateTemplateRequest(validTemplate);
        var json = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/v1/n8n/templates/validate", httpContent);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ValidateTemplateResponse>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.True(result!.Valid);
    }

    #region Helper Methods

    private async Task<HttpClient> CreateAuthenticatedClient(
        string email = "admin@meepleai.dev",
        string role = "Admin")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Login to get session cookie
        var loginRequest = new
        {
            email,
            password = "Demo123!"
        };

        var loginJson = JsonSerializer.Serialize(loginRequest);
        var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

        var loginResponse = await client.PostAsync("/api/v1/auth/login", loginContent);
        loginResponse.EnsureSuccessStatusCode();

        // Extract session cookie
        if (loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var sessionCookie = string.Join("; ", cookies);
            client.DefaultRequestHeaders.Add("Cookie", sessionCookie);
        }

        return client;
    }

    #endregion
}
