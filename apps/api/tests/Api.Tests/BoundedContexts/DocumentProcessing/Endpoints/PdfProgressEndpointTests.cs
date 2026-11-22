using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Endpoints;

/// <summary>
/// Host e database una volta per classe (#4050): ogni test semina un PDF suo.
/// </summary>
public sealed class PdfProgressHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "pdf_progress");

/// <summary>
/// #5106 — <c>GET /api/v1/pdfs/{id}/progress</c> rispondeva 200 con corpo vuoto per un PDF senza progressi registrati
/// (un PDF seminato, o elaborato prima che il pipeline li scrivesse), e lo stato d'indicizzazione delle schermate admin
/// diventava «Errore nel recupero dello stato». Ora il passo si ricava dallo stato del PDF. I test asseriscono la forma
/// sul filo che legge <c>ProcessingProgressSchema</c> (<c>pdf.schemas.ts</c>).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "5106")]
public sealed class PdfProgressEndpointTests : IClassFixture<PdfProgressHostFixture>, IAsyncLifetime
{
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000510601");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string _adminToken = null!;

    public PdfProgressEndpointTests(PdfProgressHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), AdminId);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Progress_OfAReadyPdfWithoutRecordedProgress_IsCompleted()
    {
        var processedAt = DateTime.UtcNow.AddMinutes(-5);
        var pdfId = await SeedPdfAsync("Ready", progressJson: null, processedAt: processedAt);

        var progress = await GetProgressAsync(pdfId);

        progress.GetProperty("currentStep").GetString().Should().Be("Completed");
        progress.GetProperty("percentComplete").GetInt32().Should().Be(100);
        // ProcessingProgressSchema: startedAt / completedAt are z.string().datetime({ offset: true }).
        progress.GetProperty("startedAt").GetString().Should().MatchRegex(@"(Z|[+-]\d{2}:\d{2})$");
        progress.GetProperty("completedAt").GetString().Should().MatchRegex(@"(Z|[+-]\d{2}:\d{2})$");
    }

    [Fact]
    public async Task Progress_OfAFailedPdfWithoutRecordedProgress_IsFailed_WithTheError()
    {
        var pdfId = await SeedPdfAsync("Failed", progressJson: null, processingError: "Extraction failed: no text layer");

        var progress = await GetProgressAsync(pdfId);

        progress.GetProperty("currentStep").GetString().Should().Be("Failed");
        progress.GetProperty("errorMessage").GetString().Should().Be("Extraction failed: no text layer");
        progress.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Progress_OfAPdfBeingIndexed_WithoutRecordedProgress_IsItsCurrentStep()
    {
        var pdfId = await SeedPdfAsync("Chunking", progressJson: null);

        var progress = await GetProgressAsync(pdfId);

        progress.GetProperty("currentStep").GetString().Should().Be("Chunking");
        progress.GetProperty("percentComplete").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Progress_RecordedByThePipeline_IsReturnedAsItIs()
    {
        // The pipeline stores the progress with the default serializer (UpdateProgressAsync).
        var recorded = JsonSerializer.Serialize(new ProcessingProgress
        {
            CurrentStep = ProcessingStep.Embedding,
            PercentComplete = 70,
            PagesProcessed = 7,
            TotalPages = 10,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
        });
        // A Ready state with a recorded progress: the recorded one wins, the state is only a fallback.
        var pdfId = await SeedPdfAsync("Ready", progressJson: recorded);

        var progress = await GetProgressAsync(pdfId);

        progress.GetProperty("currentStep").GetString().Should().Be("Embedding");
        progress.GetProperty("percentComplete").GetInt32().Should().Be(70);
        progress.GetProperty("pagesProcessed").GetInt32().Should().Be(7);
    }

    private async Task<JsonElement> GetProgressAsync(Guid pdfId)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/pdfs/{pdfId}/progress", _adminToken), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().NotBeNullOrWhiteSpace("an empty 200 is what the admin screens read as an error");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private async Task<Guid> SeedPdfAsync(
        string processingState, string? progressJson, DateTime? processedAt = null, string? processingError = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Gioco 5106 {Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rulebook-5106.pdf",
            FilePath = $"/tmp/tests/{pdfId}.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = AdminId,
            UploadedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessingState = processingState,
            ProcessedAt = processedAt,
            ProcessingError = processingError,
            ProcessingProgressJson = progressJson,
            Language = "en",
            IsActiveForRag = true,
            LicenseType = 0,
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync(Ct);

        return pdfId;
    }
}
