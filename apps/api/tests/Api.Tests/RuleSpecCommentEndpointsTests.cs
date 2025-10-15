using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for RuleSpec comment endpoints.
///
/// Feature: RuleSpec Comments with role-based access control
/// As an editor or admin
/// I want to add, view, edit, and delete comments on RuleSpec versions
/// So that I can collaborate with other users on rule specifications
/// </summary>
public class RuleSpecCommentEndpointsTests : IntegrationTestBase
{
    public RuleSpecCommentEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Editor creates comment on RuleSpec version
    ///   Given editor user is authenticated
    ///   And a game with RuleSpec version exists
    ///   When editor posts a comment to the version
    ///   Then comment is created with HTTP 201
    ///   And comment is persisted in database
    /// </summary>
    [Fact]
    public async Task PostComment_CreatesComment_ForEditor()
    {
        // Given: Editor user is authenticated
        var user = await CreateTestUserAsync("comment-editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(user.Email);

        // And: A game with RuleSpec version exists
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, user.Id, "v1");

        // When: Editor posts a comment to the version
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/games/{game.Id}/rulespec/versions/{ruleSpec.Version}/comments")
        {
            Content = JsonContent.Create(new CreateRuleSpecCommentRequest(
                AtomId: "atom-1",
                CommentText: "This rule needs clarification"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Comment is created with HTTP 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var comment = await response.Content.ReadFromJsonAsync<RuleSpecComment>();
        Assert.NotNull(comment);
        Assert.Equal("This rule needs clarification", comment!.CommentText);
        Assert.Equal("atom-1", comment.AtomId);
        Assert.Equal(user.Id, comment.UserId);

        // And: Comment is persisted in database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await db.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == comment.Id);
        Assert.NotNull(entity);
        Assert.Equal("This rule needs clarification", entity!.CommentText);
    }

    /// <summary>
    /// Scenario: Admin creates version-level comment (without AtomId)
    ///   Given admin user is authenticated
    ///   And a game with RuleSpec version exists
    ///   When admin posts a version-level comment
    ///   Then comment is created without AtomId
    /// </summary>
    [Fact]
    public async Task PostComment_CreatesVersionLevelComment_ForAdmin()
    {
        // Given: Admin user is authenticated
        var user = await CreateTestUserAsync("comment-admin", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);

        // And: A game with RuleSpec version exists
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, user.Id, "v1");

        // When: Admin posts a version-level comment
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/games/{game.Id}/rulespec/versions/{ruleSpec.Version}/comments")
        {
            Content = JsonContent.Create(new CreateRuleSpecCommentRequest(
                AtomId: null,
                CommentText: "Overall, this version looks good"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Comment is created without AtomId
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var comment = await response.Content.ReadFromJsonAsync<RuleSpecComment>();
        Assert.NotNull(comment);
        Assert.Null(comment!.AtomId);
        Assert.Equal("Overall, this version looks good", comment.CommentText);
    }

    /// <summary>
    /// Scenario: Regular user attempts to create comment
    ///   Given regular user (non-editor/non-admin) is authenticated
    ///   When user attempts to post a comment
    ///   Then request is forbidden (HTTP 403)
    /// </summary>
    [Fact]
    public async Task PostComment_ReturnsForbidden_ForUserRole()
    {
        // Given: Regular user is authenticated
        var user = await CreateTestUserAsync("regular-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // And: A game with RuleSpec version exists (created by admin)
        var admin = await CreateTestUserAsync("admin-for-game", UserRole.Admin);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, admin.Id, "v1");

        // When: User attempts to post a comment
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/games/{game.Id}/rulespec/versions/{ruleSpec.Version}/comments")
        {
            Content = JsonContent.Create(new CreateRuleSpecCommentRequest(
                AtomId: null,
                CommentText: "Unauthorized comment"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Request is forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Get comments for a RuleSpec version
    ///   Given multiple comments exist for a version
    ///   When editor requests comments for the version
    ///   Then all comments are returned ordered by creation time
    /// </summary>
    [Fact]
    public async Task GetComments_ReturnsCommentsOrderedByCreatedAt()
    {
        // Given: Multiple comments exist for a version
        var editor = await CreateTestUserAsync("editor-1", UserRole.Editor);
        var admin = await CreateTestUserAsync("admin-1", UserRole.Admin);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, editor.Id, "v1");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.RuleSpecComments.AddRange(
                new RuleSpecCommentEntity
                {
                    GameId = game.Id,
                    Version = ruleSpec.Version,
                    AtomId = "atom-1",
                    UserId = editor.Id,
                    CommentText = "First comment",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new RuleSpecCommentEntity
                {
                    GameId = game.Id,
                    Version = ruleSpec.Version,
                    AtomId = null,
                    UserId = admin.Id,
                    CommentText = "Second comment",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new RuleSpecCommentEntity
                {
                    GameId = game.Id,
                    Version = ruleSpec.Version,
                    AtomId = "atom-2",
                    UserId = editor.Id,
                    CommentText = "Third comment",
                    CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        // When: Editor requests comments for the version
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/games/{game.Id}/rulespec/versions/{ruleSpec.Version}/comments");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: All comments are returned ordered by creation time
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RuleSpecCommentsResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.TotalComments);
        Assert.Collection(
            result.Comments,
            c => Assert.Equal("First comment", c.CommentText),
            c => Assert.Equal("Second comment", c.CommentText),
            c => Assert.Equal("Third comment", c.CommentText));
    }

    /// <summary>
    /// Scenario: Owner updates their comment
    ///   Given a comment exists
    ///   When owner updates the comment
    ///   Then comment is updated successfully
    ///   And UpdatedAt timestamp is set
    /// </summary>
    [Fact]
    public async Task UpdateComment_UpdatesComment_WhenOwner()
    {
        // Given: A comment exists
        var editor = await CreateTestUserAsync("comment-owner", UserRole.Editor);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, editor.Id, "v1");

        Guid commentId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var comment = new RuleSpecCommentEntity
            {
                GameId = game.Id,
                Version = ruleSpec.Version,
                AtomId = "atom-1",
                UserId = editor.Id,
                CommentText = "Original text",
                CreatedAt = DateTime.UtcNow
            };
            db.RuleSpecComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        // When: Owner updates the comment
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/games/{game.Id}/rulespec/comments/{commentId}")
        {
            Content = JsonContent.Create(new UpdateRuleSpecCommentRequest("Updated text"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Comment is updated successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RuleSpecComment>();
        Assert.NotNull(updated);
        Assert.Equal("Updated text", updated!.CommentText);
        Assert.NotNull(updated.UpdatedAt);
    }

    /// <summary>
    /// Scenario: Non-owner attempts to update comment
    ///   Given a comment exists owned by another user
    ///   When a different editor tries to update it
    ///   Then request is forbidden (HTTP 403)
    /// </summary>
    [Fact]
    public async Task UpdateComment_ReturnsForbidden_WhenNotOwner()
    {
        // Given: A comment exists owned by another user
        var owner = await CreateTestUserAsync("owner", UserRole.Editor);
        var otherEditor = await CreateTestUserAsync("other-editor", UserRole.Editor);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, owner.Id, "v1");

        Guid commentId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var comment = new RuleSpecCommentEntity
            {
                GameId = game.Id,
                Version = ruleSpec.Version,
                AtomId = "atom-1",
                UserId = owner.Id,
                CommentText = "Owner's comment",
                CreatedAt = DateTime.UtcNow
            };
            db.RuleSpecComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        // When: A different editor tries to update it
        var cookies = await AuthenticateUserAsync(otherEditor.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/games/{game.Id}/rulespec/comments/{commentId}")
        {
            Content = JsonContent.Create(new UpdateRuleSpecCommentRequest("Hijacked text"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Request is forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Owner deletes their comment
    ///   Given a comment exists
    ///   When owner deletes the comment
    ///   Then comment is deleted successfully (HTTP 204)
    ///   And comment is removed from database
    /// </summary>
    [Fact]
    public async Task DeleteComment_DeletesComment_WhenOwner()
    {
        // Given: A comment exists
        var editor = await CreateTestUserAsync("comment-owner", UserRole.Editor);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, editor.Id, "v1");

        Guid commentId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var comment = new RuleSpecCommentEntity
            {
                GameId = game.Id,
                Version = ruleSpec.Version,
                AtomId = "atom-1",
                UserId = editor.Id,
                CommentText = "To be deleted",
                CreatedAt = DateTime.UtcNow
            };
            db.RuleSpecComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        // When: Owner deletes the comment
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/games/{game.Id}/rulespec/comments/{commentId}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Comment is deleted successfully
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // And: Comment is removed from database
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var deleted = await db.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId);
            Assert.Null(deleted);
        }
    }

    /// <summary>
    /// Scenario: Admin deletes any comment
    ///   Given a comment exists owned by another user
    ///   When admin deletes the comment
    ///   Then comment is deleted successfully
    /// </summary>
    [Fact]
    public async Task DeleteComment_DeletesComment_WhenAdmin()
    {
        // Given: A comment exists owned by another user
        var editor = await CreateTestUserAsync("editor-owner", UserRole.Editor);
        var admin = await CreateTestUserAsync("admin-deleter", UserRole.Admin);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, editor.Id, "v1");

        Guid commentId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var comment = new RuleSpecCommentEntity
            {
                GameId = game.Id,
                Version = ruleSpec.Version,
                AtomId = null,
                UserId = editor.Id,
                CommentText = "Editor's comment",
                CreatedAt = DateTime.UtcNow
            };
            db.RuleSpecComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        // When: Admin deletes the comment
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/games/{game.Id}/rulespec/comments/{commentId}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Comment is deleted successfully
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var deleted = await db.RuleSpecComments.FirstOrDefaultAsync(c => c.Id == commentId);
            Assert.Null(deleted);
        }
    }

    /// <summary>
    /// Scenario: Non-owner non-admin attempts to delete comment
    ///   Given a comment exists owned by another user
    ///   When a different editor tries to delete it
    ///   Then request is forbidden (HTTP 403)
    /// </summary>
    [Fact]
    public async Task DeleteComment_ReturnsForbidden_WhenNotOwnerAndNotAdmin()
    {
        // Given: A comment exists owned by another user
        var owner = await CreateTestUserAsync("owner", UserRole.Editor);
        var otherEditor = await CreateTestUserAsync("other-editor", UserRole.Editor);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, owner.Id, "v1");

        Guid commentId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var comment = new RuleSpecCommentEntity
            {
                GameId = game.Id,
                Version = ruleSpec.Version,
                AtomId = "atom-1",
                UserId = owner.Id,
                CommentText = "Protected comment",
                CreatedAt = DateTime.UtcNow
            };
            db.RuleSpecComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.Id;
        }

        // When: A different editor tries to delete it
        var cookies = await AuthenticateUserAsync(otherEditor.Email);
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/games/{game.Id}/rulespec/comments/{commentId}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Request is forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated user attempts to access comments
    ///   Given no user is authenticated
    ///   When requesting comments
    ///   Then request is unauthorized (HTTP 401)
    /// </summary>
    [Fact]
    public async Task GetComments_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Given: A game with RuleSpec exists
        var admin = await CreateTestUserAsync("admin", UserRole.Admin);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, admin.Id, "v1");

        // When: Unauthenticated user requests comments
        var client = CreateClientWithoutCookies();
        var response = await client.GetAsync(
            $"/api/v1/games/{game.Id}/rulespec/versions/{ruleSpec.Version}/comments");

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Create comment with invalid data
    ///   Given editor user is authenticated
    ///   When posting a comment with empty text
    ///   Then request returns BadRequest (HTTP 400)
    /// </summary>
    [Fact]
    public async Task PostComment_ReturnsBadRequest_WhenCommentTextEmpty()
    {
        // Given: Editor user is authenticated
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, editor.Id, "v1");

        // When: Posting a comment with empty text
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/games/{game.Id}/rulespec/versions/{ruleSpec.Version}/comments")
        {
            Content = JsonContent.Create(new CreateRuleSpecCommentRequest(
                AtomId: null,
                CommentText: ""))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Request returns BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Create comment for non-existent version
    ///   Given editor user is authenticated
    ///   When posting a comment for a version that doesn't exist
    ///   Then request returns BadRequest (HTTP 400)
    /// </summary>
    [Fact]
    public async Task PostComment_ReturnsBadRequest_WhenVersionNotFound()
    {
        // Given: Editor user is authenticated
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var game = await CreateTestGameAsync($"Chess-{Guid.NewGuid():N}"[..20]);

        // When: Posting a comment for a non-existent version
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/games/{game.Id}/rulespec/versions/v999/comments")
        {
            Content = JsonContent.Create(new CreateRuleSpecCommentRequest(
                AtomId: null,
                CommentText: "Comment on non-existent version"))
        };
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Request returns BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
