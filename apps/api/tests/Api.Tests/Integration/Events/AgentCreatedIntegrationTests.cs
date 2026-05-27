using System.Net;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

/// <summary>
/// Integration test: CreateUserAgentCommand emits <c>agent.created</c> to
/// <c>domain_event_logs</c> atomically with the agent row.
/// BE-3 #1590 — H1: user-facing flow only (quick-create / POST /agents/user).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentCreatedIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _webFactory = null!;
    private HttpClient _client = null!;

    public AgentCreatedIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"agent_created_events_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _webFactory = IntegrationWebApplicationFactory.Create(connectionString);
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
        _client = _webFactory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_webFactory is not null)
            await _webFactory.DisposeAsync();
        if (!string.IsNullOrEmpty(_testDbName))
            await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    /// <summary>
    /// POST /api/v1/agents/quick-create → CreateUserAgentCommand → domain_event_logs row
    /// with EventType="agent.created", AggregateType="AgentCreated", PayloadVersion=1,
    /// payload containing camelCase gameId and gameName.
    /// </summary>
    [Fact]
    public async Task CreateUserAgent_ViaQuickCreate_LogsAgentCreatedEvent_InDomainEventLogs()
    {
        // Arrange
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Agent");

        var createRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/quick-create",
            sessionToken,
            new { gameId });

        // Act
        var response = await _client.SendAsync(createRequest);

        // Assert — HTTP success first
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        // Re-fetch from a fresh scope to read post-commit state (avoids first-level-cache hits)
        using var verifyScope = _webFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "agent.created" && e.UserId == userId)
            .OrderByDescending(e => e.LoggedAt)
            .FirstOrDefaultAsync();

        logRow.Should().NotBeNull(
            "CreateUserAgentCommand must emit agent.created to domain_event_logs — BE-3 #1590");

        // EventType alias registered in EventTypeRegistry
        logRow!.EventType.Should().Be("agent.created");

        // UserId — populated by DomainEventLogMapper reflection on event.UserId property
        logRow.UserId.Should().Be(userId);

        // AggregateType — mapper derives from class name by stripping "Event" suffix:
        // AgentCreatedEvent → "AgentCreated"
        logRow.AggregateType.Should().Be("AgentCreated");

        // AggregateId — mapper reads event.AggregateId property (the agent id)
        logRow.AggregateId.Should().NotBeNull();
        logRow.AggregateId.Should().NotBe(Guid.Empty);

        // PayloadVersion — v1 schema (BE-3 #1590)
        logRow.PayloadVersion.Should().Be(1);

        // PayloadJson — DomainEventLogMapper uses JsonNamingPolicy.CamelCase
        logRow.PayloadJson.Should().Contain("\"gameId\"")
            .And.Contain(gameId.ToString());
        logRow.PayloadJson.Should().Contain("\"gameName\"")
            .And.Contain("Catan-BE3-Agent");
    }

    /// <summary>
    /// POST /api/v1/agents/user → CreateUserAgentCommand directly → domain_event_logs row.
    /// Verifies the same handler path is covered regardless of the calling route.
    /// </summary>
    [Fact]
    public async Task CreateUserAgent_ViaUserRoute_LogsAgentCreatedEvent_InDomainEventLogs()
    {
        // Arrange
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Wingspan-BE3-Agent");

        var createRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/agents/user",
            sessionToken,
            new { gameId, agentType = "Tutor" });

        // Act
        var response = await _client.SendAsync(createRequest);

        // Assert — HTTP success
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        using var verifyScope = _webFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "agent.created" && e.UserId == userId)
            .OrderByDescending(e => e.LoggedAt)
            .FirstOrDefaultAsync();

        logRow.Should().NotBeNull(
            "POST /agents/user must also emit agent.created — BE-3 #1590 H1");
        logRow!.EventType.Should().Be("agent.created");
        logRow.PayloadJson.Should().Contain("\"gameName\"").And.Contain("Wingspan-BE3-Agent");
    }
}
