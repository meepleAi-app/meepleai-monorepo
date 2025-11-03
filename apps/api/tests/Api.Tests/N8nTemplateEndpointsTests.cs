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
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTemplates_ReturnsTemplates_WhenAuthenticated()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        var templates = JsonSerializer.Deserialize<List<WorkflowTemplateDto>>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        templates.Should().NotBeNull();
        // Should have templates from infra/n8n/templates/
        templates.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTemplates_FiltersByCategory_WhenCategoryProvided()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates?category=integration");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        var templates = JsonSerializer.Deserialize<List<WorkflowTemplateDto>>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        templates.Should().NotBeNull();
        templates!.Should().OnlyContain(t => t.Category == "integration");
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
        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        var template = JsonSerializer.Deserialize<WorkflowTemplateDetailDto>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        template.Should().NotBeNull();
        template!.Id.Should().Be(templateId);
        template.Workflow.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var client = await CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates/nonexistent-template");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ToLower().Should().Contain("required");
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ToLower().Should().Contain("not found");
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.IsSuccessStatusCode.Should().BeTrue();
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ValidateTemplateResponse>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Valid.Should().BeTrue();
    }

    #region Helper Methods

    private async Task<HttpClient> CreateAuthenticatedClient(
        string email = "admin@meepleai.dev",
        string role = "Admin")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
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
