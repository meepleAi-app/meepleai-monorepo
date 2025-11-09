using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

[Collection("Postgres")]
public class N8nTemplateEndpointsTests : AdminTestFixture, IClassFixture<PostgresCollectionFixture>, IClassFixture<WebApplicationFactoryFixture>
{
    private readonly ITestOutputHelper _output;

    public N8nTemplateEndpointsTests(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory, ITestOutputHelper output)
        : base(postgresFixture, factory)
    {
        _output = output;
    }

    [Fact]
    public async Task GetTemplates_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = CreateClientWithoutCookies();

        // Act
        var response = await client.GetAsync("/api/v1/n8n/templates");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTemplates_ReturnsTemplates_WhenAuthenticated()
    {
        // Arrange
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/n8n/templates");
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

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
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/n8n/templates?category=integration");
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

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
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");
        var templateId = "bgg-game-sync"; // Known template from seed data

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/n8n/templates/{templateId}");
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

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
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/n8n/templates/nonexistent-template");
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ImportTemplate_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = CreateClientWithoutCookies();
        var importRequest = new ImportTemplateRequest(new Dictionary<string, string>());
        var json = JsonSerializer.Serialize(importRequest);
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
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");

        // bgg-game-sync template requires bggUsername, syncInterval, apiBaseUrl
        var importRequest = new ImportTemplateRequest(new Dictionary<string, string>
        {
            { "syncInterval", "0 2 * * *" }
            // Missing required: bggUsername, apiBaseUrl
        });

        var json = JsonSerializer.Serialize(importRequest);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/n8n/templates/bgg-game-sync/import")
        {
            Content = httpContent
        };
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ToLower().Should().Contain("required");
    }

    [Fact]
    public async Task ImportTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");

        var importRequest = new ImportTemplateRequest(new Dictionary<string, string>());
        var json = JsonSerializer.Serialize(importRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/n8n/templates/nonexistent/import")
        {
            Content = content
        };
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.ToLower().Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateTemplate_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var client = CreateClientWithoutCookies();
        var validateRequest = new ValidateTemplateRequest("{}");
        var json = JsonSerializer.Serialize(validateRequest);
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
        var client = CreateClientWithoutCookies();
        var email = $"n8n-user-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "User");

        var validateRequest = new ValidateTemplateRequest("{}");
        var json = JsonSerializer.Serialize(validateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/n8n/templates/validate")
        {
            Content = content
        };
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ValidateTemplate_ReturnsValidationResult_ForAdmin()
    {
        // Arrange
        var client = CreateClientWithoutCookies();
        var email = $"n8n-admin-{Guid.NewGuid():N}@test.local";
        var cookies = await RegisterAndAuthenticateAsync(client, email, "Admin");

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

        var validateRequest = new ValidateTemplateRequest(validTemplate);
        var json = JsonSerializer.Serialize(validateRequest);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/n8n/templates/validate")
        {
            Content = httpContent
        };
        AddCookies(request, cookies);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ValidateTemplateResponse>(
            responseContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Valid.Should().BeTrue();
    }
}
