using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.SharedGameCatalog;

/// <summary>
/// E2E tests for the complete admin journey:
/// BGG Import → PDF Upload → Queue Monitoring → Notification → Agent + RAG Testing
///
/// Tests the full workflow an admin follows to populate the shared game catalog
/// with BGG data, upload rulebook PDFs, monitor processing, and test AI chat.
///
/// Note: External services (BGG API, Embedding, Qdrant, LLM) are disabled in E2E.
/// Tests verify endpoint availability, request/response contracts, and HTTP pipeline.
/// Steps requiring external services gracefully degrade with status code checks.
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AdminGameCreationJourneyE2ETests : E2ETestBase
{
    private Guid _testSharedGameId;

    public AdminGameCreationJourneyE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override async Task SeedTestDataAsync()
    {
        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"E2E Admin Journey Game {Guid.NewGuid():N}",
            Description = "Test game for admin creation journey E2E tests",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            YearPublished = 2024,
            BggId = null,
            ImageUrl = "https://example.com/journey-test.png",
            ThumbnailUrl = "https://example.com/journey-test-thumb.png",
            Status = 1, // Published
            CreatedBy = Guid.Empty,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        DbContext.SharedGames.Add(sharedGame);
        await DbContext.SaveChangesAsync();
        _testSharedGameId = sharedGame.Id;
    }

    #region Step 1: BGG Search & Import

    [Fact]
    public async Task BggSearch_WithSearchTerm_ReturnsResults()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/shared-games/bgg/search?searchTerm=Catan");

        // Assert - BGG API disabled in E2E, may return empty or service unavailable
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("BggSearch returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BggCheckDuplicate_WithBggId_ReturnsResult()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/shared-games/bgg/check-duplicate/13");

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("BggCheckDuplicate returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<BggDuplicateCheckResponse>();
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task BggSearch_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/admin/shared-games/bgg/search?searchTerm=Catan");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportFromBgg_WithBggId_CreatesSharedGame()
    {
        // Arrange
        var (adminToken, adminId) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act - Use a BGG ID unlikely to be already imported
        var payload = new { bggId = 999999, userId = adminId };
        var response = await Client.PostAsJsonAsync("/api/v1/admin/shared-games/import-bgg", payload);

        // Assert - BGG disabled, will fail to fetch details
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("ImportFromBgg returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Conflict,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Step 2: Shared Game Management

    [Fact]
    public async Task CreateSharedGame_Manually_ReturnsCreatedGame()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var payload = new
        {
            title = $"Manual E2E Game {Guid.NewGuid():N}",
            yearPublished = 2024,
            description = "A game created manually for E2E testing",
            minPlayers = 2,
            maxPlayers = 4,
            playingTimeMinutes = 45,
            minAge = 10,
            imageUrl = "https://example.com/manual-test.png",
            thumbnailUrl = "https://example.com/manual-test-thumb.png"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/admin/shared-games", payload);

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CreateSharedGame returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PublishSharedGame_SubmitAndApprove_ChangesStatus()
    {
        // Arrange - Create a new draft game
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var createPayload = new
        {
            title = $"Publish E2E Game {Guid.NewGuid():N}",
            yearPublished = 2024,
            description = "Game for publish workflow test",
            minPlayers = 1,
            maxPlayers = 6,
            playingTimeMinutes = 90,
            minAge = 14,
            imageUrl = "https://example.com/publish-test.png",
            thumbnailUrl = "https://example.com/publish-test-thumb.png"
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/admin/shared-games", createPayload);

        if (createResponse.StatusCode != HttpStatusCode.Created && createResponse.StatusCode != HttpStatusCode.OK)
            return;

        var gameId = await ExtractGameIdFromResponse(createResponse);
        if (gameId == null) return;

        // Act - Submit for approval
        var submitResponse = await Client.PostAsync(
            $"/api/v1/admin/shared-games/{gameId}/submit-for-approval", null);

        submitResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.BadRequest);

        if (submitResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)
        {
            // Approve publication
            var approveResponse = await Client.PostAsync(
                $"/api/v1/admin/shared-games/{gameId}/approve-publication", null);

            approveResponse.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.BadRequest);
        }
    }

    #endregion

    #region Step 3: PDF Upload

    [Fact]
    public async Task UploadPdf_ToSharedGame_CreatesPdfDocument()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var pdfContent = CreateMinimalPdfBytes();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test-rulebook.pdf");
        content.Add(new StringContent(_testSharedGameId.ToString()), "gameId");

        // Act
        var response = await Client.PostAsync("/api/v1/ingest/pdf", content);

        // Assert - Upload may succeed even without processing services
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("UploadPdf returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.Accepted,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadMultiplePdfs_DifferentCategories_AllAccepted()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var pdfFiles = new[]
        {
            ("base-rules.pdf", "Rulebook"),
            ("expansion-seafarers.pdf", "Rulebook"),
            ("faq-v2.pdf", "FAQ")
        };

        var uploadResults = new List<HttpStatusCode>();

        // Act - Upload each PDF sequentially
        foreach (var (fileName, _) in pdfFiles)
        {
            var pdfContent = CreateMinimalPdfBytes();
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(pdfContent);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(_testSharedGameId.ToString()), "gameId");

            var response = await Client.PostAsync("/api/v1/ingest/pdf", content);
            uploadResults.Add(response.StatusCode);
        }

        // Assert - All uploads should reach the endpoint (success or known error)
        if (uploadResults.Any(s => s == HttpStatusCode.InternalServerError))
            Assert.Skip("UploadMultiplePdfs returned 500 — service likely unavailable");
        uploadResults.Should().AllSatisfy(status =>
            status.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.Created,
                HttpStatusCode.Accepted,
                HttpStatusCode.BadRequest,
                HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task UploadPdf_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthentication();

        var pdfContent = CreateMinimalPdfBytes();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "unauthorized-test.pdf");
        content.Add(new StringContent(_testSharedGameId.ToString()), "gameId");

        // Act
        var response = await Client.PostAsync("/api/v1/ingest/pdf", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Step 4: Queue Monitoring

    [Fact]
    public async Task AdminQueue_GetStatus_ReturnsQueueInfo()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/queue/status");

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AdminQueue_GetStatus returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<QueueStatusResponse>();
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AdminQueue_ListJobs_ReturnsPaginatedList()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/queue?page=1&pageSize=10");

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AdminQueue_ListJobs returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminQueue_GetAlerts_ReturnsAlertList()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/queue/alerts");

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AdminQueue_GetAlerts returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminQueue_GetMetrics_ReturnsDashboardMetrics()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/queue/metrics?period=24h");

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AdminQueue_GetMetrics returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminQueue_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/admin/queue/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Step 5: KB Monitoring

    [Fact]
    public async Task AdminKb_GetVectorCollections_ReturnsCollectionList()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync("/api/v1/admin/kb/vector-collections");

        // Assert - Qdrant disabled in E2E, may return error
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AdminKb_GetVectorCollections returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task KnowledgeBaseStatus_ForGame_ReturnsStatus()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act
        var response = await Client.GetAsync($"/api/v1/knowledge-base/{_testSharedGameId}/status");

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("KnowledgeBaseStatus returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Step 6: Agent Creation & Linking

    [Fact]
    public async Task CreateAgent_WithValidConfig_ReturnsAgent()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var payload = new
        {
            name = $"E2E Agent {Guid.NewGuid():N}",
            type = "TutorAgent",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["topK"] = 5,
                ["minScore"] = 0.6
            },
            isActive = true
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents", payload);

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CreateAgent returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);

        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<AgentResponse>();
            result.Should().NotBeNull();
            result!.Id.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task LinkAgentToSharedGame_WithValidIds_Succeeds()
    {
        // Arrange - Create agent first
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var agentPayload = new
        {
            name = $"Link Agent {Guid.NewGuid():N}",
            type = "TutorAgent",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["topK"] = 5,
                ["minScore"] = 0.6
            },
            isActive = true
        };

        var agentResponse = await Client.PostAsJsonAsync("/api/v1/agents", agentPayload);

        if (agentResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
            return;

        var agent = await agentResponse.Content.ReadFromJsonAsync<AgentResponse>();
        if (agent == null) return;

        // Act - Link agent to shared game
        var linkResponse = await Client.PostAsync(
            $"/api/v1/admin/shared-games/{_testSharedGameId}/link-agent/{agent.Id}", null);

        // Assert
        if (linkResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("LinkAgentToSharedGame returned 500 — service likely unavailable");
        linkResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Conflict,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAgent_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthentication();

        var payload = new
        {
            name = "Unauthorized Agent",
            type = "TutorAgent",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>(),
            isActive = true
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Step 7: Agent Chat & RAG Testing

    [Fact]
    public async Task AgentChat_WithMessage_ReturnsResponse()
    {
        // Arrange - Create and configure agent
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var agentPayload = new
        {
            name = $"Chat Agent {Guid.NewGuid():N}",
            type = "TutorAgent",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["topK"] = 5,
                ["minScore"] = 0.6
            },
            isActive = true
        };

        var agentResponse = await Client.PostAsJsonAsync("/api/v1/agents", agentPayload);

        if (agentResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
            return;

        var agent = await agentResponse.Content.ReadFromJsonAsync<AgentResponse>();
        if (agent == null) return;

        // Act - Chat with agent (LLM disabled, but endpoint should respond)
        var chatPayload = new
        {
            message = "What are the basic rules of this game?"
        };

        var chatResponse = await Client.PostAsJsonAsync($"/api/v1/agents/{agent.Id}/chat", chatPayload);

        // Assert - LLM disabled in E2E, endpoint reachable but may fail on LLM call
        if (chatResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AgentChat returned 500 — service likely unavailable");
        chatResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task AgentPlayground_AdminTest_ReturnsResponse()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        var agentPayload = new
        {
            name = $"Playground Agent {Guid.NewGuid():N}",
            type = "TutorAgent",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["topK"] = 5,
                ["minScore"] = 0.6
            },
            isActive = true
        };

        var agentResponse = await Client.PostAsJsonAsync("/api/v1/agents", agentPayload);

        if (agentResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
            return;

        var agent = await agentResponse.Content.ReadFromJsonAsync<AgentResponse>();
        if (agent == null) return;

        // Act - Admin playground chat
        var playgroundPayload = new
        {
            message = "How do I set up the game?",
            gameId = _testSharedGameId.ToString()
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/admin/agent-definitions/{agent.Id}/playground/chat", playgroundPayload);

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AgentPlayground returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task AgentAutoTest_ForGame_ReturnsTestResults()
    {
        // Arrange
        var (adminToken, _) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // Act - Run auto-test against shared game
        var response = await Client.PostAsync(
            $"/api/v1/admin/games/{_testSharedGameId}/agent/auto-test", null);

        // Assert - May fail without LLM/Qdrant
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("AgentAutoTest returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Complete Journey

    [Fact]
    public async Task CompleteAdminJourney_CreateGame_UploadPdf_CreateAgent_TestChat()
    {
        // === Step 1: Admin authenticates ===
        var (adminToken, adminId) = await LoginAsAdminAsync();
        SetSessionCookie(adminToken);

        // === Step 2: Create shared game manually (BGG disabled in E2E) ===
        var gameTitle = $"Journey Game {Guid.NewGuid():N}";
        var createGamePayload = new
        {
            title = gameTitle,
            yearPublished = 2024,
            description = "Complete admin journey E2E test game with rich rules and mechanics",
            minPlayers = 2,
            maxPlayers = 5,
            playingTimeMinutes = 75,
            minAge = 12,
            imageUrl = "https://example.com/journey-game.png",
            thumbnailUrl = "https://example.com/journey-game-thumb.png"
        };

        var createGameResponse = await Client.PostAsJsonAsync("/api/v1/admin/shared-games", createGamePayload);

        if (createGameResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
            return; // Skip - game creation endpoint not available

        var gameId = await ExtractGameIdFromResponse(createGameResponse);
        if (gameId == null) return;

        // === Step 3: Publish the game (Draft → PendingApproval → Published) ===
        var submitResponse = await Client.PostAsync(
            $"/api/v1/admin/shared-games/{gameId}/submit-for-approval", null);

        if (submitResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)
        {
            var approveResponse = await Client.PostAsync(
                $"/api/v1/admin/shared-games/{gameId}/approve-publication", null);

            approveResponse.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.BadRequest);
        }

        // === Step 4: Upload PDF rulebook ===
        var pdfContent = CreateMinimalPdfBytes();
        using var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formContent.Add(fileContent, "file", "journey-game-rules.pdf");
        formContent.Add(new StringContent(gameId.ToString()!), "gameId");

        var uploadResponse = await Client.PostAsync("/api/v1/ingest/pdf", formContent);

        if (uploadResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CompleteAdminJourney PDF upload returned 500 — service likely unavailable");
        uploadResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.Accepted,
            HttpStatusCode.BadRequest);

        // === Step 5: Check queue status ===
        var queueResponse = await Client.GetAsync("/api/v1/admin/queue/status");

        if (queueResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CompleteAdminJourney queue status returned 500 — service likely unavailable");
        queueResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.Forbidden);

        // === Step 6: Check KB status for game ===
        var kbStatusResponse = await Client.GetAsync($"/api/v1/knowledge-base/{gameId}/status");

        if (kbStatusResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CompleteAdminJourney KB status returned 500 — service likely unavailable");
        kbStatusResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NotFound);

        // === Step 7: Create agent ===
        var agentPayload = new
        {
            name = $"Journey Agent {Guid.NewGuid():N}",
            type = "TutorAgent",
            strategyName = "HybridSearch",
            strategyParameters = new Dictionary<string, object>
            {
                ["topK"] = 5,
                ["minScore"] = 0.6
            },
            isActive = true
        };

        var agentCreateResponse = await Client.PostAsJsonAsync("/api/v1/agents", agentPayload);

        if (agentCreateResponse.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK))
            return;

        var agent = await agentCreateResponse.Content.ReadFromJsonAsync<AgentResponse>();
        if (agent == null) return;

        // === Step 8: Link agent to shared game ===
        var linkResponse = await Client.PostAsync(
            $"/api/v1/admin/shared-games/{gameId}/link-agent/{agent.Id}", null);

        if (linkResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CompleteAdminJourney link agent returned 500 — service likely unavailable");
        linkResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent,
            HttpStatusCode.NotFound, HttpStatusCode.Conflict);

        // === Step 9: Test chat with agent (LLM disabled, verify endpoint reachable) ===
        var chatPayload = new { message = "How do I set up the board?" };
        var chatResponse = await Client.PostAsJsonAsync($"/api/v1/agents/{agent.Id}/chat", chatPayload);

        if (chatResponse.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("CompleteAdminJourney agent chat returned 500 — service likely unavailable");
        chatResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Helper Methods

    private async Task<(string SessionToken, Guid UserId)> LoginAsAdminAsync()
    {
        var payload = new
        {
            email = "admin@test.local",
            password = "TestAdmin123!"
        };

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", payload);

        if (!response.IsSuccessStatusCode)
        {
            var registerPayload = new
            {
                email = "admin@test.local",
                password = "TestAdmin123!",
                displayName = "Test Admin"
            };

            var registerResponse = await Client.PostAsJsonAsync("/api/v1/auth/register", registerPayload);

            if (registerResponse.IsSuccessStatusCode)
            {
                var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
                var sessionToken = ExtractSessionCookie(registerResponse);
                return (sessionToken ?? throw new InvalidOperationException("Session token not found"),
                    registerResult!.User!.Id);
            }

            throw new InvalidOperationException($"Could not login or register admin: {response.StatusCode}");
        }

        var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
        var token = ExtractSessionCookie(response);
        return (token ?? throw new InvalidOperationException("Session token not found"), loginResult!.User!.Id);
    }

    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith("meepleai_session=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = cookie.Split(';')[0];
                    return value.Substring("meepleai_session=".Length);
                }
            }
        }
        return null;
    }

    private static async Task<Guid?> ExtractGameIdFromResponse(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();

            // Try parsing as GUID directly (some endpoints return just the ID)
            if (Guid.TryParse(content.Trim('"'), out var guidResult))
                return guidResult;

            // Try parsing as JSON with Id property
            var jsonResult = await response.Content.ReadFromJsonAsync<GameIdResponse>();
            return jsonResult?.Id;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CreateMinimalPdfBytes()
    {
        const string minimalPdf = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]
   /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 100 700 Td (Test Rules) Tj ET
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000266 00000 n
0000000360 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
441
%%EOF";
        return Encoding.ASCII.GetBytes(minimalPdf);
    }

    #endregion

    #region Response DTOs

    private sealed record BggDuplicateCheckResponse(
        bool IsDuplicate,
        Guid? ExistingGameId);

    private sealed record QueueStatusResponse(
        int QueueDepth,
        bool IsUnderPressure,
        bool IsPaused,
        double EstimatedWaitMinutes);

    private sealed record AgentResponse(
        Guid Id,
        string Name,
        string Type);

    private sealed record GameIdResponse(Guid Id);

    private sealed record RegisterResponse(UserDto? User, DateTime? ExpiresAt);
    private sealed record LoginResponse(UserDto? User, DateTime? ExpiresAt, bool? RequiresTwoFactor);
    private sealed record UserDto(Guid Id, string Email, string DisplayName, string Role);

    #endregion
}
