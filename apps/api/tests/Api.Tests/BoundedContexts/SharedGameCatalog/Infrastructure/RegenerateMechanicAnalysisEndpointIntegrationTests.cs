using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// La fixture di <see cref="RegenerateMechanicAnalysisEndpointIntegrationTests"/>: host condiviso
/// per la classe, database fresco per test (#4050).
/// </summary>
/// <remarks>
/// I due doppi hanno stato e vivono quanto la classe: la classe di test li azzera in
/// <c>InitializeAsync</c>, dopo <c>BeginTestAsync</c>.
/// </remarks>
public sealed class RegenerateMechanicAnalysisHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "mech_regen_4144")
{
    /// <summary>Il pipeline LLM non parte mai: l'accodamento viene solo registrato.</summary>
    internal Mock<IBackgroundTaskService> BackgroundTasks { get; } = CreateBackgroundTaskMock();

    /// <summary>La riga che una richiesta concorrente scrive fra il controllo e l'INSERT.</summary>
    public CompetingAnalysisInsert CompetingInsert { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBackgroundTaskService>();
            services.AddSingleton(BackgroundTasks.Object);

            // La stima del costo gira DOPO il controllo di idempotenza e PRIMA dell'INSERT: e' il
            // punto in cui una richiesta concorrente puo' aver gia' scritto la sua riga.
            services.RemoveAll<IAnalysisCostEstimator>();
            services.AddScoped<IAnalysisCostEstimator>(_ =>
                new InterleavingCostEstimator(new AnalysisCostEstimator(), CompetingInsert));
        }));

    private static Mock<IBackgroundTaskService> CreateBackgroundTaskMock()
    {
        var mock = new Mock<IBackgroundTaskService>();
        mock.Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()));
        return mock;
    }
}

/// <summary>
/// Una scrittura che parte una sola volta, al prossimo calcolo del costo: simula la richiesta
/// concorrente che vince la corsa sull'indice unico.
/// </summary>
public sealed class CompetingAnalysisInsert
{
    private Func<Task>? _pending;

    public void Arm(Func<Task> insert) => _pending = insert;

    public void Reset() => _pending = null;

    internal void RunIfArmed()
    {
        var pending = Interlocked.Exchange(ref _pending, null);
        // Estimate e' sincrono; nell'host di test non c'e' SynchronizationContext, quindi aspettare
        // qui non puo' andare in stallo.
        pending?.Invoke().GetAwaiter().GetResult();
    }
}

internal sealed class InterleavingCostEstimator(IAnalysisCostEstimator inner, CompetingAnalysisInsert competing)
    : IAnalysisCostEstimator
{
    public AnalysisCostEstimate Estimate(AnalysisCostEstimateInput input)
    {
        competing.RunIfArmed();
        return inner.Estimate(input);
    }
}

/// <summary>
/// #4144: l'azione Regenerate (<c>ForceRegenerate: true</c> su <c>POST /admin/mechanic-analyses</c>)
/// davanti all'indice unico filtrato <c>ux_mechanic_analyses_shared_game_pdf_prompt</c>, che ammette
/// una sola analisi con <c>status &lt;&gt; Rejected</c> per (gioco, PDF, prompt).
/// </summary>
/// <remarks>
/// Serve Postgres vero: con un repository finto il 23505 non esiste, ed e' il motivo per cui i test
/// unit non vedevano il 500.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RegenerateMechanicAnalysisEndpointIntegrationTests
    : IClassFixture<RegenerateMechanicAnalysisHostFixture>, IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/mechanic-analyses";

    private static readonly Guid TestAdminId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly RegenerateMechanicAnalysisHostFixture _host;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminToken = null!;
    private string _currentPrompt = null!;

    public RegenerateMechanicAnalysisEndpointIntegrationTests(RegenerateMechanicAnalysisHostFixture host)
    {
        _host = host;
    }

    public async ValueTask InitializeAsync()
    {
        await _host.BeginTestAsync();
        _host.BackgroundTasks.Invocations.Clear();
        _host.CompetingInsert.Reset();
        _factory = _host.Factory;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            // #4164: senza prezzo nel catalogo modelli l'estrattore risponde 409 prima dell'INSERT.
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SeedAiModelsCommand());
            (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, TestAdminId);
            _currentPrompt = scope.ServiceProvider.GetRequiredService<IMechanicPromptProvider>().PromptVersion;
        }

        _client = _factory.CreateClient();
    }

    // Host e database appartengono alla fixture; il client e' di questa istanza.
    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(MechanicAnalysisStatus.Draft, "ancora in generazione")]
    [InlineData(MechanicAnalysisStatus.InReview, "in revisione")]
    [InlineData(MechanicAnalysisStatus.PartiallyExtracted, "parzialmente estratta")]
    [InlineData(MechanicAnalysisStatus.Published, "pubblicata")]
    public async Task Regenerate_WithAnalysisForCurrentPromptInsideTheUniqueIndex_Returns409(
        MechanicAnalysisStatus existingStatus,
        string expectedHint)
    {
        // PDF collegato e indicizzato: senza la guardia la richiesta arriva fino all'INSERT
        // (23505 → 500) invece di fermarsi a un 404/409 precedente.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var existingId = await SeedAnalysisAsync(gameId, pdfId, existingStatus);

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain(expectedHint);
        body.Should().Contain(existingId.ToString(), "il messaggio dice quale analisi blocca la rigenerazione");

        (await CountAnalysesAsync(gameId)).Should().Be(1, "nessuna nuova analisi viene creata");
        VerifyPipelineEnqueued(Times.Never());
    }

    [Fact]
    public async Task Regenerate_AfterTheAnalysisForCurrentPromptWasRejected_Returns202WithANewAnalysis()
    {
        // Il consiglio del 409 («rifiutala prima») deve funzionare: Rejected sta fuori dall'indice.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var rejectedId = await SeedAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Rejected);

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, body);
        var dto = JsonSerializer.Deserialize<MechanicAnalysisGenerationResponseDto>(body, JsonOptions);
        dto.Should().NotBeNull();
        dto!.IsExistingAnalysis.Should().BeFalse();
        dto.Id.Should().NotBe(rejectedId);
        dto.Status.Should().Be(MechanicAnalysisStatus.Draft);

        (await CountAnalysesAsync(gameId)).Should().Be(2);
        VerifyPipelineEnqueued(Times.Once());
    }

    [Fact]
    public async Task Generate_WhenAConcurrentRequestInsertsFirst_ReturnsTheWinnerAs202()
    {
        // La corsa fra due richieste: entrambe passano il controllo di idempotenza, una sola INSERT
        // passa l'indice. La perdente riceve 23505, che e' una DbUpdateException e NON una
        // DbUpdateConcurrencyException: deve comunque restituire la vincitrice, come una richiesta
        // arrivata un istante dopo.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var winnerId = Guid.NewGuid();
        _host.CompetingInsert.Arm(() => InsertAnalysisAsync(winnerId, gameId, pdfId, MechanicAnalysisStatus.Draft));

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: false);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, body);
        var dto = JsonSerializer.Deserialize<MechanicAnalysisGenerationResponseDto>(body, JsonOptions);
        dto.Should().NotBeNull();
        dto!.IsExistingAnalysis.Should().BeTrue();
        dto.Id.Should().Be(winnerId);

        (await CountAnalysesAsync(gameId)).Should().Be(1);
        VerifyPipelineEnqueued(Times.Never());
    }

    [Fact]
    public async Task Regenerate_WhenAConcurrentRequestInsertsFirst_Returns409()
    {
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var winnerId = Guid.NewGuid();
        _host.CompetingInsert.Arm(() => InsertAnalysisAsync(winnerId, gameId, pdfId, MechanicAnalysisStatus.Draft));

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("ancora in generazione");
        body.Should().Contain(winnerId.ToString());

        (await CountAnalysesAsync(gameId)).Should().Be(1);
        VerifyPipelineEnqueued(Times.Never());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendGenerateAsync(Guid gameId, Guid pdfId, bool forceRegenerate)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            EndpointBase,
            _adminToken,
            new { sharedGameId = gameId, pdfDocumentId = pdfId, costCapUsd = 1.00m, forceRegenerate });
        return await _client.SendAsync(request);
    }

    private void VerifyPipelineEnqueued(Times times) =>
        _host.BackgroundTasks.Verify(
            b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            times);

    private async Task<int> CountAnalysesAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.MechanicAnalyses.AsNoTracking().IgnoreQueryFilters().CountAsync(a => a.SharedGameId == gameId);
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameWithIndexedPdfAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Regenerate Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            YearPublished = 2024,
            MinAge = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId
        });

        var pdfId = Guid.NewGuid();
        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rulebook.pdf",
            FilePath = $"/tmp/tests/{pdfId}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestAdminId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            IsActiveForRag = true,
            LicenseType = 0,
            DocumentCategory = "Rulebook"
        });

        for (var i = 0; i < 2; i++)
        {
            db.Set<TextChunkEntity>().Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = $"Rulebook chunk {i}: each turn consists of draw, action, resolve and cleanup phases.",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 90,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Set<SharedGameDocumentEntity>().Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId,
            ApprovalStatus = 1 // Approved
        });

        await db.SaveChangesAsync();
        return (gameId, pdfId);
    }

    private async Task<Guid> SeedAnalysisAsync(Guid gameId, Guid pdfId, MechanicAnalysisStatus status)
    {
        var id = Guid.NewGuid();
        await InsertAnalysisAsync(id, gameId, pdfId, status);
        return id;
    }

    // Uno scope suo, quindi un DbContext e una connessione diversi da quelli della richiesta: la riga
    // e' committata quando l'handler fa il suo INSERT, come quella di un altro amministratore.
    private async Task InsertAnalysisAsync(Guid id, Guid gameId, Guid pdfId, MechanicAnalysisStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = DateTime.UtcNow;
        var reviewed = status is MechanicAnalysisStatus.InReview
            or MechanicAnalysisStatus.Published
            or MechanicAnalysisStatus.Rejected;

        db.Set<MechanicAnalysisEntity>().Add(new MechanicAnalysisEntity
        {
            Id = id,
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            PromptVersion = _currentPrompt,
            Status = (int)status,
            CreatedBy = TestAdminId,
            CreatedAt = now,
            ReviewedBy = reviewed ? TestAdminId : null,
            ReviewedAt = reviewed ? now : null,
            RejectionReason = status == MechanicAnalysisStatus.Rejected ? "Rifiutata per il test" : null,
            TotalTokensUsed = 1234,
            EstimatedCostUsd = 0.05m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        await db.SaveChangesAsync();
    }
}
