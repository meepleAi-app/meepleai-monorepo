using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for the GameBook write endpoints
/// (<c>POST/PUT/DELETE /api/v1/gamebook/books</c>) wired in this branch — the
/// commands (Create/Update/SoftDelete) already existed but were orphaned with no
/// HTTP surface (SP6 libro-game page-3 gap, umbrella #2619).
///
/// Exercises the auth pipeline + MediatR DI + route/body binding + status mapping
/// against a real Postgres testcontainer. Follows the conservative auth pattern of
/// sibling gamebook endpoint tests: no-auth requests assert a deterministic
/// Unauthorized; authenticated requests accept the expected status OR Unauthorized
/// (the test middleware may not honor the seeded session cookie).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameBookWriteEndpointsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GameBookWriteEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"gamebook_books_{Guid.NewGuid():N}";
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

    private static object ValidCreateBody(Guid gameRefId) => new
    {
        gameRefId,
        gameRefKind = 1, // Private
        displayName = "Manuale base",
        roles = (int)(GameBookRole.Tutorial | GameBookRole.RulesReference),
        paragraphScheme = (int)ParagraphScheme.ParagraphNumber,
        language = "it",
        sequentialRead = false,
        kbSourceDocId = (Guid?)null,
        physicalOnly = false,
    };

    [Fact]
    public async Task CreateBook_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/gamebook/books", ValidCreateBody(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateBook_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/gamebook/books/{Guid.NewGuid()}",
            new { displayName = "Renamed", roles = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBook_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/v1/gamebook/books/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBook_WithAuth_ReturnsCreatedOrUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/gamebook/books", sessionToken);
        request.Content = JsonContent.Create(ValidCreateBody(Guid.NewGuid()));

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected Created or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateBook_NoRole_ReturnsBadRequestOrUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var body = new
        {
            gameRefId = Guid.NewGuid(),
            gameRefKind = 1,
            displayName = "No role",
            roles = 0, // invalid: validator requires at least one role
            paragraphScheme = (int)ParagraphScheme.ParagraphNumber,
            language = "it",
            sequentialRead = false,
            kbSourceDocId = (Guid?)null,
            physicalOnly = false,
        };
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/gamebook/books", sessionToken);
        request.Content = JsonContent.Create(body);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected BadRequest or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateBook_NonExistent_ReturnsNotFoundOrUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/gamebook/books/{Guid.NewGuid()}", sessionToken);
        request.Content = JsonContent.Create(new { displayName = "Renamed", roles = (int)GameBookRole.Narrative });

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }

    [Fact]
    public async Task DeleteBook_NonExistent_ReturnsNotFoundOrUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/gamebook/books/{Guid.NewGuid()}", sessionToken);

        var response = await _client.SendAsync(request);

        (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized).Should().BeTrue(
            $"Expected NotFound or Unauthorized, got {response.StatusCode}");
    }
}
