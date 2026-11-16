using System.Net;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// La fixture di <see cref="GenerateRuleSpecEndpointErrorMappingTests"/>: host e database per classe. Ogni test
/// semina un PDF suo, con un id nuovo, e legge solo la risposta.
/// </summary>
public sealed class GenerateRuleSpecEndpointHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "rulespec_error_mapping")
{
    /// <summary>
    /// Quando è valorizzata, il comando lancia questa eccezione prima di arrivare al gestore: è il «qualunque
    /// <see cref="InvalidOperationException"/> sotto il comando» che il vecchio <c>catch</c> dell'endpoint
    /// trasformava in un 400. Vuota, il comportamento lascia passare la richiesta.
    /// </summary>
    internal Exception? ThrowUnderTheCommand { get; set; }

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(this);
            services.AddTransient<IPipelineBehavior<GenerateRuleSpecFromPdfCommand, RuleSpecDto>, ThrowingBehavior>();
        }));

    private sealed class ThrowingBehavior(GenerateRuleSpecEndpointHostFixture fixture)
        : IPipelineBehavior<GenerateRuleSpecFromPdfCommand, RuleSpecDto>
    {
        public Task<RuleSpecDto> Handle(
            GenerateRuleSpecFromPdfCommand request, RequestHandlerDelegate<RuleSpecDto> next, CancellationToken cancellationToken) =>
            fixture.ThrowUnderTheCommand is { } ex ? throw ex : next();
    }
}

/// <summary>
/// #5014 — <c>POST /api/v1/ingest/pdf/{pdfId}/rulespec</c> e i suoi errori.
/// </summary>
/// <remarks>
/// Il gestore lanciava <see cref="InvalidOperationException"/> per un PDF senza regole, e l'endpoint la
/// intercettava rispondendo 400 col messaggio. Quel <c>catch</c> prendeva qualunque eccezione di quel tipo sotto il
/// comando: un difetto vero arrivava al client come errore della richiesta, col testo interno.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "5014")]
public sealed class GenerateRuleSpecEndpointErrorMappingTests
    : IClassFixture<GenerateRuleSpecEndpointHostFixture>, IAsyncLifetime
{
    private readonly GenerateRuleSpecEndpointHostFixture _host;
    private Guid _adminId;
    private string _adminToken = null!;

    public GenerateRuleSpecEndpointErrorMappingTests(GenerateRuleSpecEndpointHostFixture host)
    {
        _host = host;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host.ThrowUnderTheCommand = null;
        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_adminId, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
    }

    public ValueTask DisposeAsync()
    {
        _host.ThrowUnderTheCommand = null;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task APdfWithoutRules_Answers409_WithItsCode()
    {
        var pdfId = await SeedPdfAsync(extractedText: "   \n  ");

        using var response = await PostAsync(pdfId);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain(PdfHasNoParsedRulesException.Code);
    }

    [Fact]
    public async Task AnotherInvalidOperationExceptionUnderTheCommand_IsA500_WithoutItsMessage()
    {
        var pdfId = await SeedPdfAsync(extractedText: "Prima regola\nSeconda regola");
        _host.ThrowUnderTheCommand = new InvalidOperationException("Sequence contains no elements (dettaglio interno)");

        using var response = await PostAsync(pdfId);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError, body);
        body.Should().NotContain("dettaglio interno", "an internal defect is not a request error, and its text stays in the log");
    }

    [Fact]
    public async Task APdfWithRules_StillAnswers200()
    {
        // Control: without the try/catch the success path is unchanged.
        var pdfId = await SeedPdfAsync(extractedText: "Prima regola\nSeconda regola", linkedToAGame: true);

        using var response = await PostAsync(pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    private Task<HttpResponseMessage> PostAsync(Guid pdfId) =>
        _host.Client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/ingest/pdf/{pdfId}/rulespec", _adminToken),
            Ct);

    private async Task<Guid> SeedPdfAsync(string extractedText, bool linkedToAGame = false)
    {
        var pdfId = Guid.NewGuid();
        using var scope = _host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        Guid? sharedGameId = null;
        if (linkedToAGame)
        {
            sharedGameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Gioco 5014 {pdfId:N}");
        }

        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            FileName = "rulebook-5014.pdf",
            FilePath = $"/tmp/tests/{pdfId}.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = _adminId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            ExtractedText = extractedText,
            LicenseType = 0,
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync(Ct);
        return pdfId;
    }
}
