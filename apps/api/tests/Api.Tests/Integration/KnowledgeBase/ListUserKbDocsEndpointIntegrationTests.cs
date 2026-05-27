using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// HTTP-level integration tests for GET /api/v1/kb-docs (BE-1 #1588).
/// Covers AC5 (401 unauthenticated) and the happy-path HTTP round-trip
/// using a real WebApplicationFactory + PostgreSQL Testcontainer.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ListUserKbDocsEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ListUserKbDocsEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"test_kb_docs_endpoint_{Guid.NewGuid():N}";
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        // Run EF migrations so the schema (including the new idx_pdf_documents_user_processed_at
        // index from Task 0) is present before any test touches the DB.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync(TestCancellationToken);
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ── AC5: unauthenticated request ─────────────────────────────────────────

    /// <summary>
    /// AC5: An anonymous request (no session cookie) must receive 401 Unauthorized.
    /// The RequireSession() middleware rejects the request before the handler runs.
    /// </summary>
    [Fact]
    public async Task Returns_401_when_unauthenticated()
    {
        var response = await _client.GetAsync("/api/v1/kb-docs", TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Happy-path HTTP round-trip ────────────────────────────────────────────

    /// <summary>
    /// An authenticated user with no uploaded docs receives a 200 with the empty
    /// KbDocsListResponse envelope (Items=[], Total=0, Page=1, PageSize=20).
    /// </summary>
    [Fact]
    public async Task Returns_200_with_empty_envelope_when_authenticated_user_has_no_docs()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext, cancellationToken: TestCancellationToken);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs",
            sessionToken);

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<KbDocsListResponse>(TestCancellationToken);
        payload.Should().NotBeNull();
        payload!.Total.Should().Be(0);
        payload.Items.Should().BeEmpty();
        payload.Page.Should().Be(1);
        payload.PageSize.Should().Be(20);
    }

    // ── pageSize=0 clamp ──────────────────────────────────────────────────────

    /// <summary>
    /// The endpoint handler clamps pageSize via Math.Clamp(0, 1, 100) = 1 before
    /// building the query, so the validator never sees pageSize=0.
    /// The response must be 200 with PageSize=1.
    /// </summary>
    [Fact]
    public async Task PageSize_zero_clamps_to_1()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext, cancellationToken: TestCancellationToken);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs?pageSize=0",
            sessionToken);

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<KbDocsListResponse>(TestCancellationToken);
        payload.Should().NotBeNull();
        payload!.PageSize.Should().Be(1);
    }

    // ── Invalid state value ───────────────────────────────────────────────────

    /// <summary>
    /// The ListUserKbDocsQueryValidator rejects unknown state values via the
    /// ValidationBehavior MediatR pipeline behavior (registered in Program.cs).
    /// state="nonsense" is not in the allowed set ["ready", "all"].
    /// ApiExceptionHandlerMiddleware maps FluentValidation.ValidationException to
    /// 422 UnprocessableEntity (see HandleFluentValidationExceptionAsync).
    /// </summary>
    [Fact]
    public async Task Returns_422_for_invalid_state_value()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext, cancellationToken: TestCancellationToken);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs?state=nonsense",
            sessionToken);

        var response = await _client.SendAsync(request, TestCancellationToken);

        // ValidationBehavior<ListUserKbDocsQuery, KbDocsListResponse> is in the MediatR
        // pipeline (Program.cs). When the validator rejects the query, the behavior throws
        // FluentValidation.ValidationException which ApiExceptionHandlerMiddleware maps to
        // 422 UnprocessableEntity (not 400 — see HandleFluentValidationExceptionAsync:150).
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
