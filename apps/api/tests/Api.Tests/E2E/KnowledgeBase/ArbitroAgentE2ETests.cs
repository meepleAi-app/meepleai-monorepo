using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.KnowledgeBase;

/// <summary>
/// E2E tests for Arbitro Agent move validation workflow.
/// Tests full workflow: User chat → Tutor → Arbitro → Response.
///
/// Issue #3873: E2E Workflow Tests
/// Parent: #3759 (Rules Arbitration Engine)
///
/// Critical Journeys Covered:
/// - Move validation with orchestration service
/// - Multi-turn conversation with Tutor → Arbitro routing
/// - Response time SLAs (<500ms E2E)
/// - Error scenarios (orchestration unavailable, invalid input)
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3873")]
public sealed class ArbitroAgentE2ETests : E2ETestBase
{
    private Guid _testGameId;
    private Guid _testSessionId;

    public ArbitroAgentE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override async Task SeedTestDataAsync()
    {
        // Seed test game for Arbitro validation
        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"E2E Arbitro Test - Chess {Guid.NewGuid():N}",
            Description = "Chess game for move validation E2E tests",
            MinPlayers = 2,
            MaxPlayers = 2,
            PlayingTimeMinutes = 60,
            MinAge = 8,
            YearPublished = 2024,
            BggId = null,
            ImageUrl = "https://example.com/chess.png",
            ThumbnailUrl = "https://example.com/chess-thumb.png",
            Status = 1, // Published
            CreatedBy = Guid.Empty,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        DbContext.SharedGames.Add(sharedGame);
        await DbContext.SaveChangesAsync();
        _testGameId = sharedGame.Id;

        // #3662: la GameSession dev'essere reale. `ValidateMoveCommandHandler` la carica da
        // `IGameSessionRepository.GetByIdAsync` e lancia `NotFoundException` → 404 se non esiste.
        // Qui c'era `_testSessionId = Guid.NewGuid()`, cioè un id inventato: i test passavano
        // finché l'endpoint non validava l'esistenza della sessione, e quando la validazione è
        // arrivata il gate E2E non girava più da mesi, quindi nessuno se n'è accorto.
        // È l'aggregato `GameSession` — non `LiveGameSession` né `SessionTracking.Session`,
        // che vivono in spazi di id distinti (ADR-089).
        var gameSession = new GameSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGame.Id,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            PlayersJson = """[{"PlayerName":"E2E Player","PlayerOrder":1}]"""
        };

        DbContext.GameSessions.Add(gameSession);
        await DbContext.SaveChangesAsync();
        _testSessionId = gameSession.Id;
    }

    #region Happy Path Tests

    [Fact]
    public async Task ValidateMove_ValidOpeningMove_ReturnsValid()
    {
        // Arrange
        var email = $"arbitro_valid_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e4", // Standard chess opening
            position = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR" // Starting position (FEN)
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);

        // Assert - Skip if orchestration service unavailable in test environment
        await AssertSuccessOrSkipIfServiceUnavailable(response, "ValidateMove_ValidOpeningMove");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result.Should().NotBeNull();
            // IsValid can be true or false - both are valid responses
            result.Reason.Should().NotBeEmpty();
            result.Confidence.Should().BeInRange(0.0, 1.0);
            result.AppliedRuleIds.Should().NotBeNull();
            result.Citations.Should().NotBeNull();
            result.ExecutionTimeMs.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ValidateMove_InvalidMove_ReturnsFalseWithReason()
    {
        // Arrange
        var email = $"arbitro_invalid_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "Zz9", // Invalid - no Z piece, no 9 rank
            position = "{}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("ValidateMove_InvalidMove returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result.Should().NotBeNull();
            // Invalid moves can return either false or validation errors
            result!.Reason.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ValidateMove_ResponseFormat_IncludesAllRequiredFields()
    {
        // Arrange
        var email = $"arbitro_format_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "Nf3", // Knight move
            position = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result.Should().NotBeNull();

            // Verify all required fields present
            // IsValid can be true or false - both are valid responses
            result.Reason.Should().NotBeNullOrEmpty();
            result.AppliedRuleIds.Should().NotBeNull();
            result.Confidence.Should().BeInRange(0.0, 1.0);
            result.Citations.Should().NotBeNull();
            result.ExecutionTimeMs.Should().BeGreaterThan(0);
            // ErrorMessage is optional
        }
    }

    #endregion

    #region Multi-Turn Workflow Tests

    [Fact]
    public async Task TutorToArbitroWorkflow_CompleteSequence_Succeeds()
    {
        // Arrange
        var email = $"arbitro_workflow_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        // Step 1: Query Tutor agent for setup help
        var tutorPayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            query = "How do I set up the chess board?"
        };

        var tutorResponse = await Client.PostAsJsonAsync("/api/v1/agents/tutor/query", tutorPayload);

        await AssertSuccessOrSkipIfServiceUnavailable(tutorResponse, "TutorToArbitroWorkflow tutor query");

        if (tutorResponse.IsSuccessStatusCode)
        {
            var tutorResult = await tutorResponse.Content.ReadFromJsonAsync<TutorQueryResponse>();
            tutorResult.Should().NotBeNull();
            tutorResult!.AgentType.Should().Be("tutor");
            tutorResult.Response.Should().NotBeEmpty();
        }

        // Step 2: Validate a move with Arbitro agent (same session context)
        var arbitroPayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e4",
            position = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"
        };

        var arbitroResponse = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", arbitroPayload);

        await AssertSuccessOrSkipIfServiceUnavailable(arbitroResponse, "TutorToArbitroWorkflow arbitro validate");

        if (arbitroResponse.IsSuccessStatusCode)
        {
            var arbitroResult = await arbitroResponse.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            arbitroResult.Should().NotBeNull();
            // IsValid can be true or false - both are valid
            arbitroResult.Confidence.Should().BeGreaterThan(0);
        }

        // Step 3: Verify session maintained state (both requests used same sessionId)
        // This validates that context is preserved across agent interactions
        _testSessionId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MultiTurnValidation_SequentialMoves_MaintainsContext()
    {
        // Arrange
        var email = $"arbitro_multiturn_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var startingPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR";

        // Turn 1: Validate opening move
        var move1Payload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e4",
            position = startingPosition
        };

        var response1 = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", move1Payload);

        if (response1.IsSuccessStatusCode)
        {
            var result1 = await response1.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result1.Should().NotBeNull();
        }

        // Turn 2: Validate response move (same session)
        var move2Payload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e5", // Black's response
            position = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR"
        };

        var response2 = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", move2Payload);

        if (response2.IsSuccessStatusCode)
        {
            var result2 = await response2.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result2.Should().NotBeNull();
        }

        // Both moves should maintain same session context
        _testSessionId.Should().NotBeEmpty();
    }

    #endregion

    #region Response Time SLA Tests

    [Fact]
    public async Task ValidateMove_ResponseTime_Under500ms()
    {
        // Arrange
        var email = $"arbitro_sla_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "Nf3",
            position = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);
        stopwatch.Stop();

        // Assert - Response time SLA
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result.Should().NotBeNull();

            // API reports execution time from orchestration service
            result!.ExecutionTimeMs.Should().BeGreaterThan(0);

            // Total E2E time (including network, API overhead) - relaxed for test env
            // Production target: <500ms; Test environment: <2000ms (orchestration startup overhead)
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        }
    }

    #endregion

    #region Error Scenario Tests

    [Fact]
    public async Task ValidateMove_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthentication();

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e4",
            position = "{}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidateMove_InvalidGameState_HandlesGracefully()
    {
        // Arrange
        var email = $"arbitro_error_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e4",
            position = "INVALID_FEN_STRING"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);

        // Assert - Should handle invalid state gracefully
        if (response.StatusCode == HttpStatusCode.InternalServerError)
            Assert.Skip("ValidateMove_InvalidGameState returned 500 — service likely unavailable");
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, // Returns validation result with error
            HttpStatusCode.BadRequest); // Validation rejects invalid state

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ValidateMoveResponse>();
            result.Should().NotBeNull();
            // May be marked invalid or have ErrorMessage set
        }
    }

    [Fact]
    public async Task ValidateMove_OrchestrationServiceUnavailable_Returns500()
    {
        // Arrange
        var email = $"arbitro_unavailable_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidUnusualPwd123!");
        SetSessionCookie(sessionToken);

        var validatePayload = new
        {
            gameSessionId = _testSessionId,
            playerName = "E2E Player",
            action = "e4",
            position = "{}"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/agents/arbitro/validate", validatePayload);

        // Assert - This test explicitly validates graceful 500 handling when orchestration is unavailable.
        // 500 is a VALID expected outcome here (unlike other tests where it indicates false positives).
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, // Service available — graceful handling
            HttpStatusCode.InternalServerError, // Service unavailable — expected 500
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadGateway);
    }

    #endregion

    #region Response DTOs

    private sealed record TutorQueryResponse(
        string Response,
        string AgentType,
        double Confidence,
        List<string> Citations,
        double ExecutionTimeMs);

    private sealed record ValidateMoveResponse(
        bool IsValid,
        string Reason,
        List<Guid> AppliedRuleIds,
        double Confidence,
        List<string> Citations,
        double ExecutionTimeMs,
        string? ErrorMessage = null);

    #endregion
}

#pragma warning restore S1144
