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
/// Integration test: CreateChatSessionCommand emits <c>chat.session.created</c> to
/// <c>domain_event_logs</c> atomically with the session row.
/// BE-3 #1590 — H2: alias "chat.session.created" matches real command name (not fictional CreateChatThreadCommand).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ChatSessionCreatedIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _webFactory = null!;
    private HttpClient _client = null!;

    public ChatSessionCreatedIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"chat_session_created_events_{Guid.NewGuid():N}";
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
    /// POST /api/v1/chat/sessions → CreateChatSessionCommand → domain_event_logs row
    /// with EventType="chat.session.created", AggregateType="ChatSessionCreated",
    /// PayloadVersion=1, payload containing camelCase gameId.
    ///
    /// Uses Admin session to satisfy RAG access check (Rule 1: Admin → always allowed),
    /// which sidesteps the need for a UserLibraryEntry or IsRagPublic flag.
    /// </summary>
    [Fact]
    public async Task CreateChatSession_LogsChatSessionCreatedEvent_InDomainEventLogs()
    {
        // Arrange — Admin session bypasses RAG access check (RagAccessService Rule 1)
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Chat");

        var createRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/chat/sessions",
            sessionToken,
            new { gameId });

        // Act
        var response = await _client.SendAsync(createRequest);

        // Assert — HTTP success
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        // Re-fetch from a fresh scope to read post-commit state (avoids first-level-cache hits)
        using var verifyScope = _webFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "chat.session.created" && e.UserId == userId)
            .OrderByDescending(e => e.LoggedAt)
            .FirstOrDefaultAsync();

        logRow.Should().NotBeNull(
            "CreateChatSessionCommand must emit chat.session.created to domain_event_logs — BE-3 #1590 H2");

        // EventType alias registered in EventTypeRegistry
        logRow!.EventType.Should().Be("chat.session.created");

        // UserId — populated by DomainEventLogMapper reflection on event.UserId property
        logRow.UserId.Should().Be(userId);

        // AggregateType — mapper derives from class name by stripping "Event" suffix:
        // ChatSessionCreatedEvent → "ChatSessionCreated"
        logRow.AggregateType.Should().Be("ChatSessionCreated");

        // AggregateId — mapper reads event.AggregateId property (= the new chat session id)
        logRow.AggregateId.Should().NotBeNull();
        logRow.AggregateId.Should().NotBe(Guid.Empty);

        // PayloadVersion — v1 schema (BE-3 #1590)
        logRow.PayloadVersion.Should().Be(1);

        // PayloadJson — DomainEventLogMapper uses JsonNamingPolicy.CamelCase
        logRow.PayloadJson.Should().Contain("\"gameId\"")
            .And.Contain(gameId.ToString());

        // BE-3 #1590 H2 payload-completeness: gameName must be present so the
        // activity rail can render a meaningful title without a secondary join.
        logRow.PayloadJson.Should().Contain("\"gameName\"")
            .And.Contain("Catan-BE3-Chat",
                because: "CreateChatSessionCommandHandler resolves gameName via ISharedGameRepository");
    }
}
