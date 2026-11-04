using System.Net;
using System.Net.Http.Json;
using Api.Filters;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.Extensions.DependencyInjection;
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
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, "test-internal-tokeX");

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
    public async Task Post_PipelineBusy_Returns202WithReason()
    {
        var pdfId = Guid.NewGuid();
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var userId = Guid.NewGuid();
            db.Users.Add(new UserEntity { Id = userId, Email = $"{userId:N}@example.com", PasswordHash = "hash", Role = "user", CreatedAt = DateTime.UtcNow });
            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                FileName = "test.pdf",
                FilePath = "/uploads/test.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingState = "Extracting",
                ExtractedText = "text",
            });
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, InternalPdfEventsEndpointsHostFixture.Token);

        var response = await _client.PostAsJsonAsync(Route, Body(pdfId));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync()).Should().Contain("pipeline_busy");
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
