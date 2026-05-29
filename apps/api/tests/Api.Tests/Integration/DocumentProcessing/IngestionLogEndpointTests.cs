using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for the GET /api/v1/admin/kb/docs/{docId}/ingestion-log endpoint.
/// Issue #1650: KB Ingestion Log tab — verifies routing, authentication, and empty-state behaviour.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1650")]
public sealed class IngestionLogEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public IngestionLogEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"ingestion_log_{Guid.NewGuid():N}";
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
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ========================================
    // GET /api/v1/admin/kb/docs/{docId}/ingestion-log
    // ========================================

    [Fact]
    public async Task GET_NoSession_Returns401()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/admin/kb/docs/{docId}/ingestion-log");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_NonExistentDoc_Returns200WithNullBody()
    {
        // Arrange — random docId that has no ProcessingJob in the DB
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var request = CreateAuthenticatedRequest(HttpMethod.Get,
            $"/api/v1/admin/kb/docs/{docId}/ingestion-log",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — Results.Ok(null) in .NET 9 Minimal APIs produces 200 with empty or "null" body
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        (body == "null" || body == string.Empty)
            .Should().BeTrue($"Expected null or empty body for non-existent doc, got: {body}");
    }

    [Fact]
    public async Task GET_EmptyGuid_Returns400OrNull()
    {
        // Arrange — Guid.Empty: model binding may reject (400) or handler short-circuits (200 null)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var request = CreateAuthenticatedRequest(HttpMethod.Get,
            $"/api/v1/admin/kb/docs/{Guid.Empty}/ingestion-log",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — either 400 (model binding rejects empty guid) or 200 null (handler short-circuit)
        (response.StatusCode == HttpStatusCode.BadRequest
         || response.StatusCode == HttpStatusCode.OK)
            .Should().BeTrue($"Expected 400 or 200, got {response.StatusCode}");
    }

    [Fact]
    public async Task GET_ExistingDocWithJob_Returns200WithIngestionLog()
    {
        // Arrange — seed a PdfDocument + ProcessingJob, then call the endpoint as admin
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);

        var docId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        dbContext.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"test-{docId:N}.pdf",
            FilePath = $"/test/test-{docId:N}.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = adminUserId,
            UploadedAt = DateTime.UtcNow,
        });
        dbContext.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = docId,
            UserId = adminUserId,
            Status = "Completed",
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var request = CreateAuthenticatedRequest(HttpMethod.Get,
            $"/api/v1/admin/kb/docs/{docId}/ingestion-log",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty("a ProcessingJob exists for the document");

        using var json = System.Text.Json.JsonDocument.Parse(body);
        var root = json.RootElement;

        root.GetProperty("pdfDocumentId").GetGuid()
            .Should().Be(docId, "response must identify the queried document");

        root.GetProperty("status").GetString()
            .Should().Be("Completed", "job was seeded with Status=Completed");
    }

    // ========================================
    // Helpers
    // ========================================

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string sessionToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={sessionToken}");
        return request;
    }
}
