using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Agents;

/// <summary>
/// Integration tests for user-facing AgentTypologies HTTP endpoint (Issue #649).
/// Verifies <c>GET /api/v1/agent-typologies</c> wraps the existing <c>GetAllAgentDefinitionsQueryHandler</c>
/// MediatR handler over HTTP with proper authentication and Published-only filter support.
///
/// "Typologies" was collapsed into AgentDefinition during system simplification —
/// this route exposes Published agent definitions as the user-facing typology dropdown source.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentTypologiesEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AgentTypologiesEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agent_typologies_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetTypologies_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/agent-typologies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTypologies_WithValidSession_ReturnsEmptyList()
    {
        // Arrange: authenticated user, no agent definitions seeded
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agent-typologies",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAgentTypologiesResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Typologies.Should().BeEmpty();
        body.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetTypologies_WithSeededAgentDefinitions_ReturnsOnlyPublished()
    {
        // Arrange: seed 2 Published + 1 Draft agent definitions
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await TestSessionHelper.SeedAgentDefinitionsByStatusAsync(
            dbContext,
            publishedCount: 2,
            draftCount: 1);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agent-typologies?status=Approved",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetAgentTypologiesResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Typologies.Should().HaveCount(2);
        body.Typologies.Should().OnlyContain(t => t.IsActive);
        body.Total.Should().Be(2);
    }
}

/// <summary>
/// HTTP response shape returned by <c>GET /api/v1/agent-typologies</c>.
/// Mirrors anonymous object created in <c>AgentTypologiesEndpoints.MapGetTypologiesEndpoint</c>.
/// </summary>
internal record GetAgentTypologiesResponse(bool Success, List<AgentDefinitionDto> Typologies, int Total);
