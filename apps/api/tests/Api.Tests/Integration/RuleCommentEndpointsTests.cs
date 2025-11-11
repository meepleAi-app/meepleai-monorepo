using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// BDD-style integration tests for RuleSpec Comment endpoints (EDIT-05).
///
/// Feature: RuleSpec Collaborative Comments
/// As an authenticated user
/// I want to create, reply to, and manage comments on rule specifications
/// So that I can collaborate with other users on rule documentation
///
/// Endpoints Tested:
/// 1. POST /api/v1/rulespecs/{gameId}/{version}/comments - Create comment
/// 2. POST /api/v1/comments/{commentId}/replies - Create reply
/// 3. GET /api/v1/rulespecs/{gameId}/{version}/comments?includeResolved={bool} - Get comments
/// 4. GET /api/v1/rulespecs/{gameId}/{version}/lines/{lineNumber}/comments - Get line comments
/// 5. POST /api/v1/comments/{commentId}/resolve - Resolve comment
/// 6. POST /api/v1/comments/{commentId}/unresolve - Unresolve comment
/// 7. GET /api/v1/users/search?query={q} - User search autocomplete
///
/// Test Strategy: Full HTTP stack with Testcontainers (Postgres + Qdrant)
/// </summary>
[Collection("Postgres Integration Tests")]
public class RuleCommentEndpointsTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public RuleCommentEndpointsTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    #region Test Data Helpers

    /// <summary>
    /// Creates a test game and rulespec for comment testing.
    /// Returns (gameId, version).
    /// </summary>
    private async Task<(string gameId, string version)> CreateTestRuleSpecAsync(Guid userId)
    {
        var game = await CreateTestGameAsync($"TestGame-{TestRunId}");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var ruleSpec = new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Version = "v1",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        db.RuleSpecs.Add(ruleSpec);
        await db.SaveChangesAsync();

        return (game.Id.ToString(), ruleSpec.Version);
    }

    private async Task<HttpResponseMessage> PostWithCookiesAsync<T>(
        List<string> cookies,
        string url,
        T payload)
    {
        var client = CreateClientWithoutCookies();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetWithCookiesAsync(
        List<string> cookies,
        string url)
    {
        var client = CreateClientWithoutCookies();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }

    #endregion

    #region Create Comment Workflow Tests

    /// <summary>
    /// Scenario: User creates a comment
    ///   Given user is authenticated
    ///   And a valid rulespec exists
    ///   When user creates a comment
    ///   Then 201 Created is returned
    ///   And Location header contains comment URL
    ///   And comment is stored in database
    /// </summary>
    [Fact]
    public async Task GivenAuthenticatedUser_WhenCreatingComment_ThenReturns201WithLocationHeader()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync($"commenter-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        // Given: Valid rulespec exists
        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        // When: User creates a comment
        var response = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "This is a test comment"));

        // Then: 201 Created is returned
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Then: Location header contains comment URL
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location.ToString().Should().Contain("/api/v1/comments/");

        // Then: Comment is stored correctly
        var comment = await response.Content.ReadFromJsonAsync<RuleCommentDto>();
        comment.Should().NotBeNull();
        comment.GameId.Should().Be(gameId);
        comment.Version.Should().Be(version);
        comment.CommentText.Should().Be("This is a test comment");
        comment.UserId.Should().Be(user.Id);
        comment.IsResolved.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: User creates a comment with lineNumber
    ///   Given user is authenticated
    ///   When user creates a comment on specific line
    ///   Then comment is stored with lineNumber
    /// </summary>
    [Fact]
    public async Task GivenAuthenticatedUser_WhenCreatingCommentWithLineNumber_ThenLineNumberStored()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync($"line-commenter-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        // When: User creates a comment on line 42
        var response = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, 42, "Comment on line 42"));

        // Then: LineNumber is stored correctly
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<RuleCommentDto>();
        comment.Should().NotBeNull();
        comment.LineNumber.Should().Be(42);
    }

    /// <summary>
    /// Scenario: User creates comment with @mentions
    ///   Given user is authenticated
    ///   When user creates comment with @user mentions
    ///   Then MentionedUserIds are populated (Note: mention parsing may be implemented client-side)
    /// </summary>
    [Fact]
    public async Task GivenAuthenticatedUser_WhenCreatingCommentWithMentions_ThenCommentCreated()
    {
        // Given: User is authenticated
        var author = await CreateTestUserAsync($"author-{TestRunId}", "user");
        var mentioned = await CreateTestUserAsync($"mentioned-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(author.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(author.Id);

        // When: User creates comment with @mention
        var response = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, $"Hey @{mentioned.Id}, check this out!"));

        // Then: Comment is created successfully
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<RuleCommentDto>();
        comment.Should().NotBeNull();
        comment.CommentText.Should().Contain("@");
    }

    #endregion

    #region Threading Workflow Tests

    /// <summary>
    /// Scenario: User creates a threaded reply
    ///   Given parent comment exists
    ///   When user creates a reply
    ///   Then 201 Created is returned
    ///   And reply is linked to parent
    /// </summary>
    [Fact]
    public async Task GivenParentComment_WhenCreatingReply_ThenReturns201WithParentLink()
    {
        // Given: Parent comment exists
        var user = await CreateTestUserAsync($"replier-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        var parentResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Parent comment"));
        var parent = await parentResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        // When: User creates a reply
        var replyResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{parent!.Id}/replies",
            new CreateReplyRequest("Reply to parent"));

        // Then: 201 Created is returned
        replyResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Then: Reply is linked to parent
        var reply = await replyResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
        reply.Should().NotBeNull();
        reply.ParentCommentId.Should().Be(parent.Id);
        reply.CommentText.Should().Be("Reply to parent");
    }

    /// <summary>
    /// Scenario: User creates nested reply chain
    ///   Given parent comment with reply exists
    ///   When user creates reply to reply
    ///   Then all replies are linked correctly
    ///   And hierarchy is preserved
    /// </summary>
    [Fact]
    public async Task GivenReplyChain_WhenCreatingNestedReply_ThenHierarchyPreserved()
    {
        // Given: Parent comment exists
        var user = await CreateTestUserAsync($"nester-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        var parentResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Parent"));
        var parent = await parentResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        // Given: First-level reply exists
        var reply1Response = await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{parent!.Id}/replies",
            new CreateReplyRequest("Reply 1"));
        var reply1 = await reply1Response.Content.ReadFromJsonAsync<RuleCommentDto>();

        // When: Creating second-level reply
        var reply2Response = await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{reply1!.Id}/replies",
            new CreateReplyRequest("Reply 2"));

        // Then: All replies succeed
        reply2Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Then: Hierarchy is correct
        var allCommentsResponse = await GetWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments?includeResolved=true");
        var allComments = await allCommentsResponse.Content.ReadFromJsonAsync<List<RuleCommentDto>>();

        allComments.Should().NotBeNull();
        allComments.Should().ContainSingle(); // Only 1 top-level comment
        allComments[0].Replies.Should().ContainSingle(); // Parent has 1 reply
        allComments[0].Replies[0].Replies.Should().ContainSingle(); // Reply1 has 1 reply
    }

    /// <summary>
    /// Scenario: User replies to non-existent comment
    ///   Given comment does not exist
    ///   When user attempts to reply
    ///   Then 404 Not Found is returned
    /// </summary>
    [Fact]
    public async Task GivenNonExistentComment_WhenCreatingReply_ThenReturns404()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync($"404-replier-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        // When: User replies to non-existent comment
        var fakeCommentId = Guid.NewGuid();
        var response = await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{fakeCommentId}/replies",
            new CreateReplyRequest("Reply to nothing"));

        // Then: 404 Not Found is returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Scenario: User exceeds maximum reply depth
    ///   Given reply chain at maximum depth
    ///   When user attempts to add another level
    ///   Then 400 Bad Request is returned
    /// </summary>
    [Fact]
    public async Task GivenMaxDepthReached_WhenCreatingReply_ThenReturns400()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync($"deep-replier-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        // Create a chain of replies up to maximum depth (10 levels)
        var parentResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Root"));
        var parent = await parentResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        Guid currentId = parent!.Id;
        for (int i = 0; i < 10; i++)
        {
            var replyResponse = await PostWithCookiesAsync(cookies,
                $"/api/v1/comments/{currentId}/replies",
                new CreateReplyRequest($"Reply level {i + 1}"));

            if (replyResponse.StatusCode != HttpStatusCode.Created)
            {
                // We've hit the limit
                replyResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                return;
            }

            var reply = await replyResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
            currentId = reply!.Id;
        }

        // When: Attempting to exceed max depth
        var exceedResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{currentId}/replies",
            new CreateReplyRequest("Reply beyond limit"));

        // Then: 400 Bad Request is returned
        exceedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Resolution Workflow Tests

    /// <summary>
    /// Scenario: User resolves a comment
    ///   Given unresolved comment exists
    ///   When user resolves the comment
    ///   Then 200 OK is returned
    ///   And comment is marked as resolved
    /// </summary>
    [Fact]
    public async Task GivenUnresolvedComment_WhenResolving_ThenReturns200AndMarkedResolved()
    {
        // Given: Unresolved comment exists
        var user = await CreateTestUserAsync($"resolver-{TestRunId}", "editor");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        var createResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "To be resolved"));
        var comment = await createResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        // When: User resolves the comment
        var resolveResponse = await PostWithCookiesAsync<object>(cookies,
            $"/api/v1/comments/{comment!.Id}/resolve?resolveReplies=false", null!);

        // Then: 200 OK is returned
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then: Comment is marked as resolved
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
        resolved.Should().NotBeNull();
        resolved.IsResolved.Should().BeTrue();
        resolved.ResolvedByUserId.Should().Be(user.Id);
        resolved.ResolvedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Scenario: User unresolves a comment
    ///   Given resolved comment exists
    ///   When user unresolves the comment
    ///   Then 200 OK is returned
    ///   And comment is marked as unresolved
    /// </summary>
    [Fact]
    public async Task GivenResolvedComment_WhenUnresolving_ThenReturns200AndMarkedUnresolved()
    {
        // Given: Resolved comment exists
        var user = await CreateTestUserAsync($"unresolver-{TestRunId}", "editor");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        var createResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "To be resolved then unresolved"));
        var comment = await createResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        await PostWithCookiesAsync<object>(cookies, $"/api/v1/comments/{comment!.Id}/resolve?resolveReplies=false", null!);

        // When: User unresolves the comment
        var unresolveResponse = await PostWithCookiesAsync<object>(cookies,
            $"/api/v1/comments/{comment.Id}/unresolve?unresolveParent=false", null!);

        // Then: 200 OK is returned
        unresolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then: Comment is marked as unresolved
        var unresolved = await unresolveResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
        unresolved.Should().NotBeNull();
        unresolved.IsResolved.Should().BeFalse();
        unresolved.ResolvedByUserId.Should().BeNull();
        unresolved.ResolvedAt.Should().BeNull();
    }

    /// <summary>
    /// Scenario: User resolves comment with replies recursively
    ///   Given comment with replies exists
    ///   When user resolves with resolveReplies=true
    ///   Then all replies are also resolved
    /// </summary>
    [Fact]
    public async Task GivenCommentWithReplies_WhenResolvingRecursively_ThenRepliesAlsoResolved()
    {
        // Given: Comment with replies exists
        var user = await CreateTestUserAsync($"recursive-resolver-{TestRunId}", "editor");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        var parentResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Parent"));
        var parent = await parentResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{parent!.Id}/replies",
            new CreateReplyRequest("Reply 1"));
        await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{parent.Id}/replies",
            new CreateReplyRequest("Reply 2"));

        // When: Resolving parent with resolveReplies=true
        var resolveResponse = await PostWithCookiesAsync<object>(cookies,
            $"/api/v1/comments/{parent.Id}/resolve?resolveReplies=true", null!);

        // Then: Parent is resolved
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
        resolved!.IsResolved.Should().BeTrue();

        // Then: All replies are also resolved
        var allCommentsResponse = await GetWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments?includeResolved=true");
        var allComments = await allCommentsResponse.Content.ReadFromJsonAsync<List<RuleCommentDto>>();

        allComments.Should().NotBeNull();
        allComments[0].Replies.Should().OnlyContain(r => r.IsResolved);
    }

    #endregion

    #region Filtering & Retrieval Tests

    /// <summary>
    /// Scenario: Get comments with includeResolved=false filters resolved
    ///   Given resolved and unresolved comments exist
    ///   When fetching comments with includeResolved=false
    ///   Then only unresolved comments are returned
    /// </summary>
    [Fact]
    public async Task GivenResolvedAndUnresolvedComments_WhenFetchingWithoutResolved_ThenOnlyUnresolvedReturned()
    {
        // Given: Resolved and unresolved comments exist
        var user = await CreateTestUserAsync($"filter-user-{TestRunId}", "editor");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        // Create unresolved comment
        var unresolvedResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Unresolved"));
        var unresolved = await unresolvedResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        // Create resolved comment
        var resolvedResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Resolved"));
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
        await PostWithCookiesAsync<object>(cookies, $"/api/v1/comments/{resolved!.Id}/resolve?resolveReplies=false", null!);

        // When: Fetching with includeResolved=false
        var filteredResponse = await GetWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments?includeResolved=false");
        var filteredComments = await filteredResponse.Content.ReadFromJsonAsync<List<RuleCommentDto>>();

        // Then: Only unresolved comment is returned
        filteredComments.Should().NotBeNull();
        filteredComments.Should().ContainSingle();
        filteredComments[0].Id.Should().Be(unresolved!.Id);
    }

    /// <summary>
    /// Scenario: Get comments with includeResolved=true shows all
    ///   Given resolved and unresolved comments exist
    ///   When fetching comments with includeResolved=true
    ///   Then all comments are returned
    /// </summary>
    [Fact]
    public async Task GivenResolvedAndUnresolvedComments_WhenFetchingWithResolved_ThenAllReturned()
    {
        // Given: Resolved and unresolved comments exist
        var user = await CreateTestUserAsync($"all-comments-user-{TestRunId}", "editor");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Unresolved"));

        var resolvedResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Resolved"));
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<RuleCommentDto>();
        await PostWithCookiesAsync<object>(cookies, $"/api/v1/comments/{resolved!.Id}/resolve?resolveReplies=false", null!);

        // When: Fetching with includeResolved=true
        var allCommentsResponse = await GetWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments?includeResolved=true");
        var allComments = await allCommentsResponse.Content.ReadFromJsonAsync<List<RuleCommentDto>>();

        // Then: All comments are returned
        allComments.Should().NotBeNull();
        allComments.Count.Should().Be(2);
    }

    /// <summary>
    /// Scenario: Get line comments filters by lineNumber
    ///   Given comments on different lines exist
    ///   When fetching comments for specific line
    ///   Then only comments for that line are returned
    /// </summary>
    [Fact]
    public async Task GivenCommentsOnDifferentLines_WhenFetchingLineComments_ThenOnlyTargetLineReturned()
    {
        // Given: Comments on different lines exist
        var user = await CreateTestUserAsync($"line-filter-user-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, 10, "Comment on line 10"));

        await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, 20, "Comment on line 20"));

        await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, 10, "Another comment on line 10"));

        // When: Fetching comments for line 10
        var lineCommentsResponse = await GetWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/lines/10/comments");
        var lineComments = await lineCommentsResponse.Content.ReadFromJsonAsync<List<RuleCommentDto>>();

        // Then: Only line 10 comments are returned
        lineComments.Should().NotBeNull();
        lineComments.Count.Should().Be(2);
        lineComments.Should().OnlyContain(c => c.LineNumber == 10);
    }

    /// <summary>
    /// Scenario: Get comments returns hierarchical structure
    ///   Given parent comments with replies exist
    ///   When fetching all comments
    ///   Then hierarchical structure is preserved
    /// </summary>
    [Fact]
    public async Task GivenThreadedComments_WhenFetchingAll_ThenHierarchyPreserved()
    {
        // Given: Threaded comments exist
        var user = await CreateTestUserAsync($"hierarchy-user-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        var parent1Response = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Parent 1"));
        var parent1 = await parent1Response.Content.ReadFromJsonAsync<RuleCommentDto>();

        await PostWithCookiesAsync(cookies,
            $"/api/v1/comments/{parent1!.Id}/replies",
            new CreateReplyRequest("Reply to parent 1"));

        await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Parent 2"));

        // When: Fetching all comments
        var allCommentsResponse = await GetWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments?includeResolved=true");
        var allComments = await allCommentsResponse.Content.ReadFromJsonAsync<List<RuleCommentDto>>();

        // Then: Hierarchical structure is preserved
        allComments.Should().NotBeNull();
        allComments.Count.Should().Be(2); // 2 top-level comments

        var parent1Result = allComments.First(c => c.Id == parent1.Id);
        parent1Result.Replies.Should().ContainSingle(); // Parent 1 has 1 reply

        var parent2Result = allComments.First(c => c.CommentText == "Parent 2");
        parent2Result.Replies.Should().BeEmpty(); // Parent 2 has no replies
    }

    #endregion

    #region User Search Tests

    /// <summary>
    /// Scenario: Search users by displayName
    ///   Given multiple users exist
    ///   When searching by displayName substring
    ///   Then matching users are returned
    /// </summary>
    [Fact]
    public async Task GivenMultipleUsers_WhenSearchingByDisplayName_ThenMatchingUsersReturned()
    {
        // Given: Multiple users exist
        var searcher = await CreateTestUserAsync($"searcher-{TestRunId}", "user");
        await CreateTestUserAsync($"john-{TestRunId}", "user"); // Display name will be "Test john-{TestRunId}"
        await CreateTestUserAsync($"jane-{TestRunId}", "user"); // Display name will be "Test jane-{TestRunId}"
        await CreateTestUserAsync($"bob-{TestRunId}", "user");

        var cookies = await AuthenticateUserAsync(searcher.Email);

        // When: Searching for "john" (part of displayName)
        var resultsResponse = await GetWithCookiesAsync(cookies, $"/api/v1/users/search?query=john-{TestRunId}");
        var results = await resultsResponse.Content.ReadFromJsonAsync<List<UserSearchResultDto>>();

        // Then: Matching users are returned
        results.Should().NotBeNull();
        results.Should().ContainSingle();
        results[0].DisplayName.Should().Contain($"john-{TestRunId}");
    }

    /// <summary>
    /// Scenario: Search users by email prefix
    ///   Given multiple users exist
    ///   When searching by email substring
    ///   Then matching users are returned
    /// </summary>
    [Fact]
    public async Task GivenMultipleUsers_WhenSearchingByEmail_ThenMatchingUsersReturned()
    {
        // Given: Multiple users exist
        var searcher = await CreateTestUserAsync($"email-searcher-{TestRunId}", "user");
        var alphaUser = await CreateTestUserAsync($"test-alpha-{TestRunId}", "user");
        await CreateTestUserAsync($"test-beta-{TestRunId}", "user");

        var cookies = await AuthenticateUserAsync(searcher.Email);

        // When: Searching for "alpha"
        var resultsResponse = await GetWithCookiesAsync(cookies, $"/api/v1/users/search?query=test-alpha-{TestRunId}");
        var results = await resultsResponse.Content.ReadFromJsonAsync<List<UserSearchResultDto>>();

        // Then: Matching users are returned
        results.Should().NotBeNull();
        results.Should().ContainSingle();
        results[0].Email.Should().Contain($"test-alpha-{TestRunId}");
    }

    #endregion

    #region Authorization Tests

    /// <summary>
    /// Scenario: Unauthenticated user attempts to create comment
    ///   Given no authentication is provided
    ///   When user attempts to create comment
    ///   Then 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task GivenNoAuthentication_WhenCreatingComment_ThenReturns401()
    {
        // Given: No authentication is provided
        var client = CreateClientWithoutCookies();

        // When: Attempting to create comment
        var response = await client.PostAsJsonAsync(
            "/api/v1/rulespecs/fake-game/v1/comments",
            new CreateCommentRequest("fake-game", "v1", null, "Unauthorized comment"));

        // Then: 401 Unauthorized is returned
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario: Authenticated user creates comment successfully
    ///   Given user is authenticated
    ///   When user creates comment
    ///   Then 201 Created is returned
    /// </summary>
    [Fact]
    public async Task GivenAuthenticatedUser_WhenCreatingComment_ThenReturns201()
    {
        // Given: User is authenticated
        var user = await CreateTestUserAsync($"auth-user-{TestRunId}", "user");
        var cookies = await AuthenticateUserAsync(user.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(user.Id);

        // When: User creates comment
        var response = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "Authenticated comment"));

        // Then: 201 Created is returned
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Scenario: Editor resolves comment successfully
    ///   Given editor is authenticated
    ///   And comment exists
    ///   When editor resolves comment
    ///   Then 200 OK is returned
    /// </summary>
    [Fact]
    public async Task GivenEditorUser_WhenResolvingComment_ThenReturns200()
    {
        // Given: Editor is authenticated
        var editor = await CreateTestUserAsync($"editor-resolver-{TestRunId}", "editor");
        var cookies = await AuthenticateUserAsync(editor.Email);

        var (gameId, version) = await CreateTestRuleSpecAsync(editor.Id);

        var createResponse = await PostWithCookiesAsync(cookies,
            $"/api/v1/rulespecs/{gameId}/{version}/comments",
            new CreateCommentRequest(gameId, version, null, "To be resolved by editor"));
        var comment = await createResponse.Content.ReadFromJsonAsync<RuleCommentDto>();

        // When: Editor resolves comment
        var resolveResponse = await PostWithCookiesAsync<object>(cookies,
            $"/api/v1/comments/{comment!.Id}/resolve?resolveReplies=false", null!);

        // Then: 200 OK is returned
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
