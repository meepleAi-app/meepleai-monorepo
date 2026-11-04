using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>Host e database per classe: ogni test crea la propria bozza.</summary>
public sealed class PdfImportWizardGameFirstHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "pdf_import_wizard");

/// <summary>
/// #4401: il wizard di import dei giochi crea il gioco PRIMA di caricare il manuale (decisione
/// dell'utente del 2026-10-15).
/// <para>
/// Prima il wizard caricava il PDF senza gioco, e l'init lo rifiutava con 422 («Either GameId or
/// PrivateGameId must be provided.»): il wizard non superava lo step 1. Anche oltre, la saga non
/// legava il PDF al gioco e mandava un comando di indicizzazione senza gestore. Ora lo step 1 crea
/// una bozza scheletro, il caricamento avviene per quella, e lo step 4 la completa.
/// </para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4401")]
public sealed class PdfImportWizardGameFirstIntegrationTests
    : IClassFixture<PdfImportWizardGameFirstHostFixture>, IAsyncLifetime
{
    private static readonly Guid ConfigOwnerId = Guid.Parse("4401c0de-0000-4000-8000-000000004401");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PdfImportWizardGameFirstIntegrationTests(PdfImportWizardGameFirstHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    /// <summary>
    /// L'init del caricamento a pezzi controlla Features.PdfUpload prima di tutto, e in un database
    /// nuovo il flag manca (fail-safe a false). Va scritto prima della prima richiesta. Idempotente.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        if (await db.Set<SystemConfigurationEntity>().AnyAsync(c => c.Key == "Features.PdfUpload", Ct))
        {
            return;
        }

        if (!await db.Users.AnyAsync(u => u.Id == ConfigOwnerId, Ct))
        {
            db.Users.Add(new UserEntity
            {
                Id = ConfigOwnerId,
                Email = "pdf-import-config-owner@meepleai.invalid",
                DisplayName = "Config owner (#4401 test)",
                Role = "admin",
                CreatedAt = DateTime.UtcNow,
            });
        }

        db.Set<SystemConfigurationEntity>().Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.PdfUpload",
            Value = "true",
            ValueType = "bool",
            Description = "#4401 test: PDF upload enabled",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = ConfigOwnerId,
        });
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Start_CreatesADraftSkeleton_ThatTheChunkedUploadAccepts()
    {
        var (adminId, token) = await AdminAsync();

        var gameId = await StartAsync(token, "Wizard Game");

        var game = await GameAsync(gameId);
        game.Title.Should().Be("Wizard Game");
        game.Status.Should().Be(0, "the wizard's game is a draft");
        game.GameDataStatus.Should().Be((int)GameDataStatus.Skeleton);
        game.CreatedBy.Should().Be(adminId);

        // Lo step 1 del wizard: oggi l'init senza gioco risponde 422, con il gioco della bozza passa.
        var init = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/ingest/pdf/chunked/init", token,
                new { gameId, fileName = "rules.pdf", totalFileSize = 2048 }),
            Ct);
        init.StatusCode.Should().Be(HttpStatusCode.OK, await init.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Import_CompletesTheDraft_AndRegistersTheRulebook()
    {
        var (adminId, token) = await AdminAsync();
        var gameId = await StartAsync(token, "Typed title");
        var pdfId = await SeedPdfAsync(gameId, adminId, "Ready");

        var response = await ImportAsync(token, gameId, pdfId, description: "A game about meeples.");

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("gameId").GetGuid().Should().Be(gameId);
        body.GetProperty("indexingStatus").GetString().Should().Be("ready");

        var game = await GameAsync(gameId);
        game.Title.Should().Be("Reviewed title");
        game.Description.Should().Be("A game about meeples.");
        game.MinPlayers.Should().Be(2);
        game.MaxPlayers.Should().Be(5);
        game.Status.Should().Be(0, "the wizard does not publish");
        game.ImageUrl.Should().BeNullOrEmpty("a blob: preview lives in the admin's browser: saved, it is a dead link");
        game.GameDataStatus.Should().Be((int)GameDataStatus.Complete,
            "left a skeleton, 'enqueue all skeletons' would overwrite the reviewed metadata from BGG");

        (await RulebooksAsync(gameId, pdfId)).Should().Be(1);
    }

    [Fact]
    public async Task Import_Retried_RegistersTheRulebookOnce()
    {
        var (adminId, token) = await AdminAsync();
        var gameId = await StartAsync(token, "Retried");
        var pdfId = await SeedPdfAsync(gameId, adminId, "Extracting");

        var first = await ImportAsync(token, gameId, pdfId, description: "First attempt.");
        var second = await ImportAsync(token, gameId, pdfId, description: "Second attempt.");

        first.StatusCode.Should().Be(HttpStatusCode.Created, await first.Content.ReadAsStringAsync(Ct));
        second.StatusCode.Should().Be(HttpStatusCode.Created, await second.Content.ReadAsStringAsync(Ct));
        (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("indexingStatus").GetString()
            .Should().Be("pending");
        (await GameAsync(gameId)).Description.Should().Be("Second attempt.");
        (await RulebooksAsync(gameId, pdfId)).Should().Be(1);
    }

    [Fact]
    public async Task Import_WithThePdfOfAnotherGame_IsAConflict_AndTouchesNothing()
    {
        var (adminId, token) = await AdminAsync();
        var gameId = await StartAsync(token, "Mine");
        var otherGameId = await StartAsync(token, "Someone else's");
        var pdfId = await SeedPdfAsync(otherGameId, adminId, "Ready");

        var response = await ImportAsync(token, gameId, pdfId, description: "Nope.");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var game = await GameAsync(gameId);
        game.Title.Should().Be("Mine");
        game.GameDataStatus.Should().Be((int)GameDataStatus.Skeleton);
        (await RulebooksAsync(gameId, pdfId)).Should().Be(0);
    }

    [Fact]
    public async Task Import_WithoutADescription_IsRejected_BeforeTouchingTheDraft()
    {
        // La saga creava il gioco con la descrizione vuota, che il dominio rifiuta: l'errore
        // arrivava dopo. Ora lo dice il validatore del comando, e la bozza resta com'è.
        var (adminId, token) = await AdminAsync();
        var gameId = await StartAsync(token, "No description");
        var pdfId = await SeedPdfAsync(gameId, adminId, "Ready");

        var response = await ImportAsync(token, gameId, pdfId, description: null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("description is required");
        (await GameAsync(gameId)).GameDataStatus.Should().Be((int)GameDataStatus.Skeleton);
        (await RulebooksAsync(gameId, pdfId)).Should().Be(0);
    }

    private async Task<(Guid UserId, string Token)> AdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateAdminSessionAsync(db, Guid.NewGuid());
    }

    private async Task<Guid> StartAsync(string token, string title)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/admin/shared-games/import-from-pdf/start", token, new { title }),
            Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("gameId").GetGuid();
    }

    private Task<HttpResponseMessage> ImportAsync(string token, Guid gameId, Guid pdfId, string? description) =>
        _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/admin/shared-games/import-from-pdf", token,
                new
                {
                    gameId,
                    pdfDocumentId = pdfId,
                    title = "Reviewed title",
                    description,
                    minPlayers = 2,
                    maxPlayers = 5,
                    // A browser blob: preview must not become the game's cover.
                    coverImageUrl = "blob:http://localhost:3000/9b1d2c3e",
                }),
            Ct);

    /// <summary>Il PDF come lo lascia il caricamento a pezzi per un gioco del catalogo (#4405).</summary>
    private async Task<Guid> SeedPdfAsync(Guid gameId, Guid uploadedBy, string processingState)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rules.pdf",
            FilePath = $"/uploads/{pdfId:N}_rules.pdf",
            UploadedByUserId = uploadedBy,
            ProcessingState = processingState,
        });
        await db.SaveChangesAsync(Ct);
        return pdfId;
    }

    private async Task<SharedGameEntity> GameAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<SharedGameEntity>().AsNoTracking().SingleAsync(g => g.Id == gameId, Ct);
    }

    private async Task<int> RulebooksAsync(Guid gameId, Guid pdfId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.SharedGameDocuments.CountAsync(d => d.SharedGameId == gameId && d.PdfDocumentId == pdfId, Ct);
    }
}
