using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>Host e database per classe: ogni test semina il proprio gioco e il proprio PDF.</summary>
public sealed class PdfCoverFromPageHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "pdf_cover_from_page");

/// <summary>
/// #4413: una pagina del regolamento del gioco diventa la sua copertina. Il wizard di import la
/// lasciava scegliere e poi la perdeva (l'anteprima era un URL <c>blob:</c> del browser).
/// </summary>
/// <remarks>
/// Lo storage di questo host è locale, e il disegno della pagina richiede R2: il caso valido arriva
/// fino al disegno e risponde 503 <c>cover_render_unavailable</c>, la stessa risposta di quando il
/// servizio di disegno è giù. Il percorso felice è in <c>SetPdfCoverFromPageCommandHandlerTests</c>.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4413")]
public sealed class PdfCoverFromPageIntegrationTests : IClassFixture<PdfCoverFromPageHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PdfCoverFromPageIntegrationTests(PdfCoverFromPageHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task AReadyPdfOfTheGame_ReachesTheRendering()
    {
        var (token, gameId, pdfId) = await SeedAsync("Ready", pageCount: 12);

        var response = await SetCoverAsync(token, gameId, pdfId, page: 3);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, await response.Content.ReadAsStringAsync(Ct));
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("cover_render_unavailable");
    }

    [Fact]
    public async Task APdfStillBeingProcessed_IsAConflict()
    {
        // Disegnare la copertina scrive la riga del PDF che l'elaborazione tiene ancora: il
        // conflitto di concorrenza la farebbe finire Failed.
        var (token, gameId, pdfId) = await SeedAsync("Embedding", pageCount: null);

        var response = await SetCoverAsync(token, gameId, pdfId, page: 1);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("still being processed");
    }

    [Fact]
    public async Task APdfOfAnotherGame_IsAConflict()
    {
        var (token, _, pdfId) = await SeedAsync("Ready", pageCount: 12);
        Guid otherGameId;
        using (var scope = _factory.Services.CreateScope())
        {
            otherGameId = await TestSessionHelper.SeedSharedGameAsync(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), $"Other {Guid.NewGuid():N}");
        }

        var response = await SetCoverAsync(token, otherGameId, pdfId, page: 1);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task APageBeyondTheEndOfThePdf_IsAConflict()
    {
        var (token, gameId, pdfId) = await SeedAsync("Ready", pageCount: 12);

        var response = await SetCoverAsync(token, gameId, pdfId, page: 13);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("12 pages");
    }

    [Fact]
    public async Task ARegularUser_IsForbidden()
    {
        var (_, gameId, pdfId) = await SeedAsync("Ready", pageCount: 12);
        string userToken;
        using (var scope = _factory.Services.CreateScope())
        {
            (_, userToken) = await TestSessionHelper.CreateUserSessionAsync(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Guid.NewGuid());
        }

        var response = await SetCoverAsync(userToken, gameId, pdfId, page: 1);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> SetCoverAsync(string token, Guid gameId, Guid pdfId, int page) =>
        _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"/api/v1/admin/shared-games/{gameId}/pdf-cover", token,
                new { pdfDocumentId = pdfId, pageNumber = page }),
            Ct);

    private async Task<(string Token, Guid GameId, Guid PdfId)> SeedAsync(string processingState, int? pageCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminId, token) = await TestSessionHelper.CreateAdminSessionAsync(db, Guid.NewGuid());
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Cover {Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rules.pdf",
            FilePath = $"/uploads/{pdfId:N}_rules.pdf",
            UploadedByUserId = adminId,
            ProcessingState = processingState,
            PageCount = pageCount,
        });
        await db.SaveChangesAsync(Ct);
        return (token, gameId, pdfId);
    }
}
