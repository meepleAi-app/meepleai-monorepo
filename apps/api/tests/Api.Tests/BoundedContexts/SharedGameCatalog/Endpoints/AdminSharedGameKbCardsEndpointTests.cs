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
/// Host e database una volta per classe (#4050): ogni test semina un gioco suo e interroga solo quello.
/// </summary>
public sealed class AdminSharedGameKbCardsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "shared_game_kb_cards");

/// <summary>
/// #5104 — <c>GET /api/v1/admin/shared-games/{id}/kb-cards</c> è nata con #4932 ed è stata tolta lo stesso giorno da
/// <c>5e87855474</c>, mentre query, gestore e <c>KbCardDto</c> restavano. La pagina admin del gioco la chiamava a ogni
/// apertura e riceveva 404. Questi test la chiamano con l'host vero e asseriscono la forma che legge
/// <c>kbCardDtoSchema</c> (<c>agent-definitions.schemas.ts</c>).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "5104")]
public sealed class AdminSharedGameKbCardsEndpointTests
    : IClassFixture<AdminSharedGameKbCardsHostFixture>, IAsyncLifetime
{
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000510401");
    private static readonly Guid EditorId = Guid.Parse("00000000-0000-0000-0000-000000510402");
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000510403");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string _adminToken = null!;
    private string _editorToken = null!;
    private string _userToken = null!;

    public AdminSharedGameKbCardsEndpointTests(AdminSharedGameKbCardsHostFixture host)
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

    private static string Route(Guid gameId, string? status = null) =>
        $"/api/v1/admin/shared-games/{gameId}/kb-cards" + (status is null ? "" : $"?status={status}");

    [Fact]
    public async Task KbCards_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync(Route(Guid.NewGuid()), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task KbCards_WithAUserSession_Returns403()
    {
        var response = await GetAsync(Route(Guid.NewGuid()), _userToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task KbCards_WithAnAdminSession_ReturnsTheCardsInTheShapeTheFrontendReads()
    {
        var seeded = await SeedGameWithCardsAsync();

        var response = await GetAsync(Route(seeded.GameId), _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var cards = body.RootElement.EnumerateArray().ToList();
        cards.Should().HaveCount(2);

        var rulebook = cards.Single(c => c.GetProperty("pdfDocumentId").GetGuid() == seeded.IndexedPdfId);
        rulebook.GetProperty("id").GetGuid().Should().NotBeEmpty();
        rulebook.GetProperty("fileName").GetString().Should().Be("rulebook-5104.pdf");
        rulebook.GetProperty("indexingStatus").GetString().Should().Be("completed");
        rulebook.GetProperty("chunkCount").GetInt32().Should().Be(42);
        // kbCardDtoSchema: z.string().datetime({ offset: true }) — una data senza fuso farebbe fallire lo schema.
        rulebook.GetProperty("indexedAt").GetString().Should().MatchRegex(@"(Z|[+-]\d{2}:\d{2})$");
        rulebook.GetProperty("documentType").GetString().Should().Be("Rulebook");
        rulebook.GetProperty("version").GetString().Should().Be("1.0");
        rulebook.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var processing = cards.Single(c => c.GetProperty("pdfDocumentId").GetGuid() == seeded.ProcessingPdfId);
        processing.GetProperty("indexingStatus").GetString().Should().Be("processing");
        processing.GetProperty("indexedAt").ValueKind.Should().Be(JsonValueKind.Null);
        // A PDF without a link in shared_game_documents still has its card, without type and version.
        processing.GetProperty("documentType").ValueKind.Should().Be(JsonValueKind.Null);
        processing.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task KbCards_FilteredByStatus_ReturnsOnlyTheMatchingCards()
    {
        var seeded = await SeedGameWithCardsAsync();

        var response = await GetAsync(Route(seeded.GameId, "completed"), _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("pdfDocumentId").GetGuid().Should().Be(seeded.IndexedPdfId);
    }

    [Fact]
    public async Task KbCards_WithAnEditorSession_Returns200_LikeTheOtherReadsOfTheGroup()
    {
        var seeded = await SeedGameWithCardsAsync();

        var response = await GetAsync(Route(seeded.GameId), _editorToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<HttpResponseMessage> GetAsync(string route, string token) =>
        await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, route, token), Ct);

    /// <summary>
    /// A game with two PDFs: the rulebook, linked as a document and indexed, and a second PDF still being indexed and
    /// not linked.
    /// </summary>
    private async Task<(Guid GameId, Guid IndexedPdfId, Guid ProcessingPdfId)> SeedGameWithCardsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Gioco 5104 {Guid.NewGuid():N}");
        var indexedPdfId = Guid.NewGuid();
        var processingPdfId = Guid.NewGuid();

        foreach (var (pdfId, fileName) in new[] { (indexedPdfId, "rulebook-5104.pdf"), (processingPdfId, "faq-5104.pdf") })
        {
            db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                FileName = fileName,
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
        }

        db.Set<SharedGameDocumentEntity>().Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            PdfDocumentId = indexedPdfId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = AdminId,
            ApprovalStatus = 1, // Approved
        });
        db.Set<VectorDocumentEntity>().Add(new VectorDocumentEntity
        {
            SharedGameId = gameId,
            PdfDocumentId = indexedPdfId,
            ChunkCount = 42,
            TotalCharacters = 4200,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
        });
        db.Set<VectorDocumentEntity>().Add(new VectorDocumentEntity
        {
            SharedGameId = gameId,
            PdfDocumentId = processingPdfId,
            IndexingStatus = "processing",
        });
        await db.SaveChangesAsync(Ct);

        return (gameId, indexedPdfId, processingPdfId);
    }
}
