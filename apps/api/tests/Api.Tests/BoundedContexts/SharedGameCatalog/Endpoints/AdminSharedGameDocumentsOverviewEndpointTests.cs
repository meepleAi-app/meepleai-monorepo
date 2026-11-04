using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Endpoints;

/// <summary>
/// Host e database una volta per classe (#4050). Condividerli è sicuro: ogni test semina un gioco con un id
/// suo e interroga solo quello, e utenti e sessioni si creano con id statici (find-or-create).
/// </summary>
public sealed class AdminSharedGameDocumentsOverviewHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "shared_game_documents_overview");

/// <summary>
/// #4579 — <c>GET /api/v1/admin/shared-games/{id}/documents/overview</c> era registrata due volte, con nomi di
/// parametro diversi (<c>{gameId:guid}</c> e <c>{sharedGameId:guid}</c>): ASP.NET lanciava
/// <c>AmbiguousMatchException</c> a ogni richiesta, cioè 500 per chiunque, admin compreso. Il test che confrontava
/// i modelli grezzi non se ne accorgeva, perché il nome del parametro fa parte del testo.
/// </summary>
/// <remarks>
/// Questi test chiamano la rotta con l'host vero. Asseriscono la forma sul filo (nomi dei campi in camelCase),
/// non i DTO: è quello che legge <c>SharedGameDocumentsResult</c> in <c>adminContentClient.ts</c> e, da lì,
/// <c>CatalogWizard</c> (<c>totalCount</c>).
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4579")]
public sealed class AdminSharedGameDocumentsOverviewEndpointTests
    : IClassFixture<AdminSharedGameDocumentsOverviewHostFixture>, IAsyncLifetime
{
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000457901");
    private static readonly Guid EditorId = Guid.Parse("00000000-0000-0000-0000-000000457902");
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000457903");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string _adminToken = null!;
    private string _editorToken = null!;
    private string _userToken = null!;

    public AdminSharedGameDocumentsOverviewEndpointTests(AdminSharedGameDocumentsOverviewHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, AdminId);
        (_, _editorToken) = await TestSessionHelper.CreateEditorSessionAsync(db, EditorId);
        (_, _userToken) = await TestSessionHelper.CreateUserSessionAsync(db, UserId);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Route(Guid gameId) => $"/api/v1/admin/shared-games/{gameId}/documents/overview";

    [Fact]
    public async Task Overview_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync(Route(Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Overview_WithAUserSession_Returns403()
    {
        var response = await GetAsync(Guid.NewGuid(), _userToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_WithAnAdminSession_ReturnsTheDocumentsInTheShapeTheFrontendReads()
    {
        var (gameId, pdfId) = await SeedGameWithRulebookAsync();

        var response = await GetAsync(gameId, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = body.RootElement;

        // SharedGameDocumentsResult: { sharedGameId, documents, totalCount }.
        root.GetProperty("sharedGameId").GetGuid().Should().Be(gameId);
        root.GetProperty("totalCount").GetInt32().Should().Be(1);

        var document = root.GetProperty("documents").EnumerateArray().Should().ContainSingle().Subject;
        document.GetProperty("pdfDocumentId").GetGuid().Should().Be(pdfId);
        document.GetProperty("sharedGameId").GetGuid().Should().Be(gameId);
        document.GetProperty("fileName").GetString().Should().Be("rulebook-4579.pdf");
        document.GetProperty("documentType").GetString().Should().Be("Rulebook");
        document.GetProperty("approvalStatus").GetString().Should().Be("Approved");
        document.GetProperty("processingState").GetString().Should().Be("Ready");
        document.GetProperty("fileSizeBytes").GetInt64().Should().Be(2048);
        document.GetProperty("isActive").GetBoolean().Should().BeTrue();
        document.GetProperty("tags").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Overview_WithAnAdminSession_OnAGameWithoutDocuments_ReturnsAnEmptyList()
    {
        // Il wizard del catalogo la chiama appena si sceglie il gioco, spesso senza documenti.
        using var scope = _factory.Services.CreateScope();
        var gameId = await TestSessionHelper.SeedSharedGameAsync(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), $"Gioco 4579 {Guid.NewGuid():N}");

        var response = await GetAsync(gameId, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("sharedGameId").GetGuid().Should().Be(gameId);
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
        body.RootElement.GetProperty("documents").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Overview_WithAnEditorSession_Returns200_LikeTheOtherReadsOfTheCatalogAdminGroup()
    {
        // Le letture sorelle del gruppo (documents, rag-readiness, cover-gap) sono per admin ed editor.
        var (gameId, _) = await SeedGameWithRulebookAsync();

        var response = await GetAsync(gameId, _editorToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid gameId, string token) =>
        await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, Route(gameId), token), Ct);

    private async Task<(Guid GameId, Guid PdfId)> SeedGameWithRulebookAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Gioco 4579 {Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rulebook-4579.pdf",
            FilePath = $"/tmp/tests/{pdfId}.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = AdminId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            IsActiveForRag = true,
            LicenseType = 0,
            DocumentCategory = "Rulebook",
        });
        db.Set<SharedGameDocumentEntity>().Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = AdminId,
            ApprovalStatus = 1, // Approved
        });
        await db.SaveChangesAsync(Ct);

        return (gameId, pdfId);
    }
}
