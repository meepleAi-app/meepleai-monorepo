using System.Net;
using System.Net.Http.Json;
using Api.Filters;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing;

public sealed class InternalPdfEventsEndpointsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "internal_pdf_events")
{
    public const string Token = "test-internal-token";

    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        [InternalServiceTokenFilter.ConfigKey] = Token,
    };
}

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class InternalPdfEventsEndpointsTests : IClassFixture<InternalPdfEventsEndpointsHostFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/internal/pdf-events/uploaded";
    private readonly InternalPdfEventsEndpointsHostFixture _hostFixture;
    private HttpClient _client = null!;

    public InternalPdfEventsEndpointsTests(InternalPdfEventsEndpointsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _client = _hostFixture.Factory.CreateClient();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object Body(Guid pdfId, string version = "v1") => new { eventId = Guid.NewGuid(), pdfDocumentId = pdfId, contractVersion = version };

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WrongToken_Returns403()
    {
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, "nope");

        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_UnknownPdf_Returns404WithOutcome()
    {
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, InternalPdfEventsEndpointsHostFixture.Token);

        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not_found");
    }

    [Fact]
    public async Task Post_UnsupportedContract_Returns400WithOutcome()
    {
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, InternalPdfEventsEndpointsHostFixture.Token);

        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid(), version: "v9"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("rejected");
    }
}
