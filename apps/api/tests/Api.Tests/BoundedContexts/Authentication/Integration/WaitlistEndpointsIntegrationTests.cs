using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Integration;

/// <summary>
/// HTTP integration tests for the public Waitlist endpoint.
/// Spec §3.5 / §4.4 (2026-04-27-v2-migration-wave-a-2-join.md) — RED phase.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class WaitlistEndpointsIntegrationTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/waitlist";
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public WaitlistEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"waitlist_endpoints_{Guid.NewGuid():N}";
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
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private static object NewPayload(
        string? email = null,
        string? name = "Alice",
        string gamePreferenceId = "g-azul",
        string? gamePreferenceOther = null,
        bool newsletterOptIn = false) => new
        {
            Email = email ?? $"alice-{Guid.NewGuid():N}@example.com",
            Name = name,
            GamePreferenceId = gamePreferenceId,
            GamePreferenceOther = gamePreferenceOther,
            NewsletterOptIn = newsletterOptIn
        };

    [Fact]
    public async Task Post_WithValidPayload_Returns200WithPositionAndEstimatedWeeks()
    {
        var payload = NewPayload();

        var response = await _client.PostAsJsonAsync(Endpoint, payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("position").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("estimatedWeeks").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Post_WithDuplicateEmail_Returns409WithExistingPosition()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var first = await _client.PostAsJsonAsync(Endpoint, NewPayload(email: email));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstPosition = firstBody.GetProperty("position").GetInt32();

        var second = await _client.PostAsJsonAsync(Endpoint, NewPayload(email: email));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("ALREADY_ON_LIST");
        body.GetProperty("position").GetInt32().Should().Be(firstPosition);
        body.GetProperty("estimatedWeeks").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Post_DuplicateEmail_CaseInsensitive_Returns409()
    {
        var email = $"case-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync(Endpoint, NewPayload(email: email.ToLowerInvariant()));

        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(email: email.ToUpperInvariant()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_WithInvalidEmail_Returns400(string email)
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(email: email));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithEmptyGamePreferenceId_Returns400()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(gamePreferenceId: ""));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithGOtherButNoFreeText_Returns400()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(
            gamePreferenceId: "g-other",
            gamePreferenceOther: null));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithGOtherAndFreeText_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(
            gamePreferenceId: "g-other",
            gamePreferenceOther: "Terraforming Mars"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WithUnknownGameId_Returns400()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(gamePreferenceId: "g-unknown-bogus"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithNameOver80Chars_Returns400()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(name: new string('a', 81)));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithGameOtherOver80Chars_Returns400()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, NewPayload(
            gamePreferenceId: "g-other",
            gamePreferenceOther: new string('x', 81)));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_AssignsMonotonicallyIncreasingPositions_AcrossSequentialCalls()
    {
        var positions = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsJsonAsync(Endpoint, NewPayload());
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            positions.Add(body.GetProperty("position").GetInt32());
        }

        positions.Should().BeInAscendingOrder();
        positions.Distinct().Count().Should().Be(positions.Count);
    }

    [Fact]
    public async Task Post_ParallelInsertsOfDistinctEmails_AllSucceedWithUniquePositions()
    {
        const int Concurrency = 10;
        var tasks = Enumerable.Range(0, Concurrency)
            .Select(_ => _client.PostAsJsonAsync(Endpoint, NewPayload()))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        var positions = new List<int>();
        foreach (var r in responses)
        {
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();
            positions.Add(body.GetProperty("position").GetInt32());
        }

        positions.Distinct().Count().Should().Be(Concurrency,
            "race-safe position assignment must yield unique values across concurrent inserts");
    }

    [Fact]
    public async Task Post_ParallelInsertsOfSameEmail_ExactlyOneSucceedsRestReturn409()
    {
        const int Concurrency = 5;
        var email = $"race-{Guid.NewGuid():N}@example.com";

        var tasks = Enumerable.Range(0, Concurrency)
            .Select(_ => _client.PostAsJsonAsync(Endpoint, NewPayload(email: email)))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(Concurrency - 1);
    }

    [Fact]
    public async Task Post_NewsletterOptInDefaultsFalse_WhenOmitted()
    {
        // Send payload without newsletterOptIn property
        var payload = new
        {
            Email = $"gdpr-{Guid.NewGuid():N}@example.com",
            Name = "Bob",
            GamePreferenceId = "g-azul"
        };

        var response = await _client.PostAsJsonAsync(Endpoint, payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // NewsletterOptIn=false stored is verified at unit/handler level;
        // this test verifies endpoint accepts payload without the field (GDPR-default-off contract).
    }

    [Fact]
    public async Task Post_NormalizesEmailToLowercase_StoresLowercase()
    {
        var rawEmail = $"Casey-{Guid.NewGuid():N}@EXAMPLE.COM";
        var first = await _client.PostAsJsonAsync(Endpoint, NewPayload(email: rawEmail));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Same email lowercase must collide
        var second = await _client.PostAsJsonAsync(Endpoint, NewPayload(email: rawEmail.ToLowerInvariant()));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
