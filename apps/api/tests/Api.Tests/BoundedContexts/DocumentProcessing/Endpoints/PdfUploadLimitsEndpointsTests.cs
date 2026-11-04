using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Endpoints;

/// <summary>
/// La fixture di <see cref="PdfUploadLimitsEndpointsTests"/>: host per classe, database per test (#4050).
/// </summary>
public sealed class PdfUploadLimitsEndpointsTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "pdf_upload_limits");

/// <summary>
/// #4449, on the real host: the request body limit of every endpoint that receives one PDF comes
/// from the single PDF limit, and <c>GET /api/v1/ingest/pdf/limits</c> answers the caller's
/// effective limit.
/// </summary>
/// <remarks>
/// Without an explicit limit Kestrel applies its default of 30 000 000 bytes: the private PDF
/// upload, the two wizards' upload and «Aggiungi RAG» refused with a bare 413 every PDF over
/// ~28.6 MB, whatever their validator allowed. The test server does not enforce body limits, so the
/// endpoints' metadata is what is asserted here; the 413/400 boundary is checked live in the PR.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfUploadLimitsEndpointsTests
    : IClassFixture<PdfUploadLimitsEndpointsTestsHostFixture>, IAsyncLifetime
{
    private const long Limit = 104_857_600; // 100 MiB
    private const long Envelope = 1024 * 1024;

    private readonly PdfUploadLimitsEndpointsTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public PdfUploadLimitsEndpointsTests(PdfUploadLimitsEndpointsTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        _client = _factory.CreateClient();
    }

    // Host e database appartengono alla fixture; il client e' di questa istanza.
    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    public static TheoryData<string, string> PdfUploadRoutes => new()
    {
        { "POST", "/api/v1/ingest/pdf" },
        { "POST", "/api/v1/users/{userId:guid}/library/entries/{entryId:guid}/pdf" },
        { "POST", "/api/v1/games/{gameId:guid}/rulebook" },
        { "POST", "/api/v1/admin/games/wizard/upload-pdf" },
        { "POST", "/api/v1/admin/shared-games/wizard/upload-pdf" },
        { "POST", "/api/v1/admin/shared-games/{id:guid}/rag" },
    };

    [Theory]
    [MemberData(nameof(PdfUploadRoutes))]
    public void APdfUploadEndpoint_AcceptsTheLimitPlusTheMultipartEnvelope(string method, string route)
    {
        var endpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => string.Equals(e.RoutePattern.RawText, route, StringComparison.Ordinal)
                && e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method) == true)
            .ToList();

        endpoints.Should().ContainSingle($"the route {method} {route} must exist: a renamed route would skip this check");
        endpoints[0].Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize
            .Should().Be(Limit + Envelope);
    }

    [Fact]
    public async Task Limits_ForAnAdmin_AreTheSingleLimit()
    {
        var json = await GetLimitsAsync(TestSessionHelper.CreateAdminSessionAsync);

        json.GetProperty("maxFileSizeBytes").GetInt64().Should().Be(Limit);
        json.GetProperty("maxConcurrentChunkedUploads").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Limits_ForAFreeUser_AreTheirPlansCap()
    {
        var json = await GetLimitsAsync(TestSessionHelper.CreateUserSessionAsync);

        json.GetProperty("maxFileSizeBytes").GetInt64().Should().Be(50L * 1024 * 1024);
    }

    [Fact]
    public async Task Limits_WithoutASession_AreRefused()
    {
        using var response = await _client.GetAsync("/api/v1/ingest/pdf/limits", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<JsonElement> GetLimitsAsync(
        Func<MeepleAiDbContext, Guid?, CancellationToken, Task<(Guid UserId, string RawToken)>> createSession)
    {
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, token) = await createSession(db, null, TestContext.Current.CancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ingest/pdf/limits");
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
