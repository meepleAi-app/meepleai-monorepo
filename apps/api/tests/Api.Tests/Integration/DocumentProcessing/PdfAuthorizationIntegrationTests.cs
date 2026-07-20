using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
/// HTTP-layer authorization tests for PDF retrieval/mutation endpoints (issue #3222).
///
/// Covers three broken-access-control gaps, each mirroring a sibling that already checks correctly:
/// - #4 IDOR: GET /api/v1/pdfs/{id}/text must not expose a private PDF's text to a non-owner
///   (mirror of GetPdfPageTextQueryHandler: shared-game PDFs public, private PDFs owner-only, 404 otherwise).
/// - #5 missing authz: PATCH /api/v1/documents/{id}/active must be owner-or-admin (mirror HandleSetPdfVisibility).
/// - #6 missing admin gate: PATCH /api/v1/documents/{id}/classify must be admin-only.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3222")]
public sealed class PdfAuthorizationIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _otherClient = null!;
    private HttpClient _adminClient = null!;
    private string _ownerToken = null!;
    private string _otherToken = null!;
    private string _adminToken = null!;
    private Guid _privatePdfId;
    private Guid _sharedPdfId;

    public PdfAuthorizationIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"pdf_authz_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        Guid ownerId;
        (ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _otherToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db);

        var sharedGameId = await TestSessionHelper.SeedSharedGameAsync(db, "Catan");

        _privatePdfId = Guid.NewGuid();
        _sharedPdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _privatePdfId,
            FileName = "private.pdf",
            FilePath = "pdfs/private.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = ownerId,
            UploadedAt = DateTime.UtcNow,
            SharedGameId = null,
            ExtractedText = "PRIVATE RULEBOOK TEXT",
            ProcessingState = "Ready",
            PageCount = 5,
            CharacterCount = 21,
        });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _sharedPdfId,
            FileName = "shared.pdf",
            FilePath = "pdfs/shared.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = ownerId,
            UploadedAt = DateTime.UtcNow,
            SharedGameId = sharedGameId,
            ExtractedText = "SHARED CATALOG RULEBOOK TEXT",
            ProcessingState = "Ready",
            PageCount = 5,
            CharacterCount = 28,
        });
        await db.SaveChangesAsync();

        _ownerClient = _factory.CreateClient();
        _otherClient = _factory.CreateClient();
        _adminClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _otherClient?.Dispose();
        _adminClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private string TextUrl(Guid id) => $"/api/v1/pdfs/{id}/text";
    private string ActiveUrl(Guid id) => $"/api/v1/documents/{id}/active";
    private string ClassifyUrl(Guid id) => $"/api/v1/documents/{id}/classify";

    // ---- #4 IDOR on GET /pdfs/{id}/text ----

    [Fact(Timeout = 90_000)]
    public async Task GetPdfText_Owner_PrivatePdf_Returns200()
    {
        var resp = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, TextUrl(_privatePdfId), _ownerToken));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetPdfText_NonOwner_PrivatePdf_Returns404()
    {
        var resp = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, TextUrl(_privatePdfId), _otherToken));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetPdfText_NonOwner_SharedGamePdf_Returns200()
    {
        var resp = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, TextUrl(_sharedPdfId), _otherToken));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetPdfText_Admin_PrivatePdf_Returns200()
    {
        // Admins bypass ownership (mirrors the admin-queue extracted-text tool).
        var resp = await _adminClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, TextUrl(_privatePdfId), _adminToken));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- #5 missing authz on PATCH /documents/{id}/active ----

    [Fact(Timeout = 90_000)]
    public async Task SetActiveForRag_Owner_Returns200()
    {
        var resp = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Patch, ActiveUrl(_privatePdfId), _ownerToken, new { isActive = false }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task SetActiveForRag_NonOwnerNonAdmin_Returns403()
    {
        var resp = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Patch, ActiveUrl(_privatePdfId), _otherToken, new { isActive = false }));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Timeout = 90_000)]
    public async Task SetActiveForRag_Admin_Returns200()
    {
        var resp = await _adminClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Patch, ActiveUrl(_privatePdfId), _adminToken, new { isActive = true }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- #6 missing admin gate on PATCH /documents/{id}/classify ----

    [Fact(Timeout = 90_000)]
    public async Task Reclassify_NonAdmin_Returns403()
    {
        var resp = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Patch, ClassifyUrl(_privatePdfId), _otherToken,
                new { category = "Rulebook", baseDocumentId = (Guid?)null, versionLabel = (string?)null }));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Timeout = 90_000)]
    public async Task Reclassify_Admin_Returns200()
    {
        var resp = await _adminClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Patch, ClassifyUrl(_privatePdfId), _adminToken,
                new { category = "Rulebook", baseDocumentId = (Guid?)null, versionLabel = (string?)null }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
