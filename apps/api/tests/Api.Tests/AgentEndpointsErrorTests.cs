using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure.Entities;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Comprehensive error case tests for Agent endpoints (/agents/qa, /agents/explain, /agents/setup, /agents/feedback).
/// Tests all possible error scenarios (400, 401).
/// Related to Issue #260 - TEST-01: Expand Integration Test Coverage.
/// </summary>
public class AgentEndpointsErrorTests : IntegrationTestBase
{
    public AgentEndpointsErrorTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    #region POST /agents/qa Error Cases

    [Fact]
    public async Task PostAgentsQa_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();
        var payload = new { gameId = "test-game", query = "What are the rules?" };

        // When: User tries to ask question without authentication
        var response = await client.PostAsJsonAsync("/agents/qa", payload);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsQa_WithEmptyGameId_ReturnsBadRequest()
    {
        // Given: Authenticated user with empty gameId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/qa");
        request.Content = JsonContent.Create(new { gameId = "", query = "What are the rules?" });
        AddCookies(request, cookies);

        // When: User tries to ask question with empty gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsQa_WithNullGameId_ReturnsBadRequest()
    {
        // Given: Authenticated user with null gameId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/qa");
        request.Content = JsonContent.Create(new { gameId = (string?)null, query = "What are the rules?" });
        AddCookies(request, cookies);

        // When: User tries to ask question with null gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsQa_WithEmptyQuery_ReturnsErrorOrBadResponse()
    {
        // Given: Authenticated user with empty query
        var user = await CreateTestUserAsync("user", UserRole.User);
        var game = await CreateTestGameAsync("Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/qa");
        request.Content = JsonContent.Create(new { gameId = game.Id, query = "" });
        AddCookies(request, cookies);

        // When: User tries to ask empty question
        var response = await client.SendAsync(request);

        // Then: System should handle gracefully (400 or proceed with empty response)
        // This is a service-level validation, might not be enforced at API level
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected BadRequest or OK, got {response.StatusCode}"
        );
    }

    #endregion

    #region POST /agents/explain Error Cases

    [Fact]
    public async Task PostAgentsExplain_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();
        var payload = new { gameId = "test-game", topic = "Setup phase" };

        // When: User tries to get explanation without authentication
        var response = await client.PostAsJsonAsync("/agents/explain", payload);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsExplain_WithEmptyGameId_ReturnsBadRequest()
    {
        // Given: Authenticated user with empty gameId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/explain");
        request.Content = JsonContent.Create(new { gameId = "", topic = "Setup phase" });
        AddCookies(request, cookies);

        // When: User tries to get explanation with empty gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsExplain_WithNullGameId_ReturnsBadRequest()
    {
        // Given: Authenticated user with null gameId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/explain");
        request.Content = JsonContent.Create(new { gameId = (string?)null, topic = "Setup phase" });
        AddCookies(request, cookies);

        // When: User tries to get explanation with null gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsExplain_WithEmptyTopic_ReturnsErrorOrBadResponse()
    {
        // Given: Authenticated user with empty topic
        var user = await CreateTestUserAsync("user", UserRole.User);
        var game = await CreateTestGameAsync("Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/explain");
        request.Content = JsonContent.Create(new { gameId = game.Id, topic = "" });
        AddCookies(request, cookies);

        // When: User tries to explain empty topic
        var response = await client.SendAsync(request);

        // Then: System should handle gracefully
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected BadRequest or OK, got {response.StatusCode}"
        );
    }

    #endregion

    #region POST /agents/setup Error Cases

    [Fact]
    public async Task PostAgentsSetup_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();
        var payload = new { gameId = "test-game" };

        // When: User tries to get setup guide without authentication
        var response = await client.PostAsJsonAsync("/agents/setup", payload);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsSetup_WithEmptyGameId_ReturnsBadRequest()
    {
        // Given: Authenticated user with empty gameId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/setup");
        request.Content = JsonContent.Create(new { gameId = "" });
        AddCookies(request, cookies);

        // When: User tries to get setup guide with empty gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsSetup_WithNullGameId_ReturnsBadRequest()
    {
        // Given: Authenticated user with null gameId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/setup");
        request.Content = JsonContent.Create(new { gameId = (string?)null });
        AddCookies(request, cookies);

        // When: User tries to get setup guide with null gameId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region POST /agents/feedback Error Cases

    [Fact]
    public async Task PostAgentsFeedback_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();
        var payload = new { userId = "user-id", messageId = "msg-123", endpoint = "qa", outcome = "helpful" };

        // When: User tries to submit feedback without authentication
        var response = await client.PostAsJsonAsync("/agents/feedback", payload);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsFeedback_WithMismatchedUserId_ReturnsBadRequest()
    {
        // Given: Authenticated user submitting feedback for different user
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/feedback");
        request.Content = JsonContent.Create(new
        {
            userId = "different-user-id",
            messageId = "msg-123",
            endpoint = "qa",
            outcome = "helpful"
        });
        AddCookies(request, cookies);

        // When: User tries to submit feedback for different user
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsFeedback_WithEmptyMessageId_ReturnsBadRequest()
    {
        // Given: Authenticated user with empty messageId
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/feedback");
        request.Content = JsonContent.Create(new
        {
            userId = user.Id,
            messageId = "",
            endpoint = "qa",
            outcome = "helpful"
        });
        AddCookies(request, cookies);

        // When: User tries to submit feedback with empty messageId
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgentsFeedback_WithEmptyEndpoint_ReturnsBadRequest()
    {
        // Given: Authenticated user with empty endpoint
        var user = await CreateTestUserAsync("user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/agents/feedback");
        request.Content = JsonContent.Create(new
        {
            userId = user.Id,
            messageId = "msg-123",
            endpoint = "",
            outcome = "helpful"
        });
        AddCookies(request, cookies);

        // When: User tries to submit feedback with empty endpoint
        var response = await client.SendAsync(request);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}
