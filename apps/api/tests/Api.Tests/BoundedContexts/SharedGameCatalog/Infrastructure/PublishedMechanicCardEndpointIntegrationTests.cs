using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test semina il proprio gioco con
/// <c>SeedGameWithCardAsync</c> (<c>gameId = Guid.NewGuid()</c>) e legge <c>GET</c> scoped a quel
/// gameId; il test del 404 usa un <c>Guid.NewGuid()</c> mai seminato. Le asserzioni sul contenuto
/// (<c>GameName</c>, <c>Sections</c>, <c>PublicationYear</c>) riguardano la card di quel singolo
/// gioco, non un aggregato: le card degli altri test non entrano nella risposta.
/// </para>
/// </summary>
public sealed class PublishedMechanicCardHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "published_mechanic_card");

/// <summary>
/// Integration tests for <c>GET /api/v1/games/{gameId}/card</c> (#528, ME-M1.6) — the login-gated
/// public read of a published mechanic card. Exercises the full stack against real PostgreSQL: the
/// content JSONB round-trip + grouping, the suppression query filter (takedown → 404), the no-card
/// 404, and the authentication gate (401 without a session).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class PublishedMechanicCardEndpointIntegrationTests
    : IClassFixture<PublishedMechanicCardHostFixture>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string _userSessionToken = null!;

    private static readonly Guid TestUserId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PublishedMechanicCardEndpointIntegrationTests(PublishedMechanicCardHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // Il seeding resta per test — xUnit crea una nuova istanza della classe per ogni [Fact] — ma
    // costruisce solo la sessione utente, non l'host. TestUserId e' static e CreateUserSessionAsync
    // e' find-or-create sull'Id: nessun 23505 sull'indice unico dell'email.
    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(dbContext, TestUserId);
        _userSessionToken = token;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetCard_ForPublishedActiveCard_Returns200WithGroupedSections()
    {
        var gameId = await SeedGameWithCardAsync(suppressed: false);

        var response = await SendGetCardAsync(gameId, authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PublishedMechanicCardDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.GameName.Should().Be("Catan");
        dto.Publisher.Should().Be("Kosmos");
        // Sections grouped + ordered by enum: Summary then Mechanics.
        dto.Sections.Select(s => s.Section).Should().Equal("Summary", "Mechanics");
        dto.Sections[0].Claims[0].Citations[0].PdfPage.Should().Be(1);
        // #530 APA context resolved at read time (year from SharedGame, doc name from the source PDF).
        dto.PublicationYear.Should().Be(1995);
        dto.DocumentName.Should().Be("Game Rules");
        dto.SourceAnalysisId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCard_ForSuppressedCard_Returns404()
    {
        var gameId = await SeedGameWithCardAsync(suppressed: true);

        var response = await SendGetCardAsync(gameId, authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCard_ForGameWithNoCard_Returns404()
    {
        var response = await SendGetCardAsync(Guid.NewGuid(), authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCard_WithoutSession_Returns401()
    {
        var gameId = await SeedGameWithCardAsync(suppressed: false);

        var response = await SendGetCardAsync(gameId, authenticated: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendGetCardAsync(Guid gameId, bool authenticated)
    {
        if (authenticated)
        {
            var request = TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Get, $"/api/v1/games/{gameId}/card", _userSessionToken);
            return await _client.SendAsync(request);
        }

        return await _client.GetAsync($"/api/v1/games/{gameId}/card");
    }

    private async Task<Guid> SeedGameWithCardAsync(bool suppressed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Catan",
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            Status = 1,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow
        });

        var analysisId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        db.Set<Api.Infrastructure.Entities.PdfDocumentEntity>().Add(new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "catan-rules.pdf",
            FilePath = "pdfs/catan-rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = TestUserId,
            UploadedAt = DateTime.UtcNow,
            Title = "Game Rules"
        });
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            PromptVersion = "mechanic-extractor-v1",
            Status = (int)Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow,
            TotalTokensUsed = 0,
            EstimatedCostUsd = 0m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        var content = new MechanicCardContent
        {
            SnapshotAt = DateTime.UtcNow,
            SourceAnalysisId = analysisId,
            SourcePromptVersion = "mechanic-extractor-v1",
            Metadata = new MechanicCardMetadata
            {
                SharedGameId = gameId,
                SharedGameName = "Catan",
                Publisher = "Kosmos",
                Language = "en"
            },
            Claims = new[]
            {
                new MechanicCardClaimSnapshot { Id = Guid.NewGuid(), Section = "Mechanics", Ordinal = 0, Claim = "Roll and produce resources.",
                    Citations = Array.Empty<MechanicCardCitationSnapshot>() },
                new MechanicCardClaimSnapshot { Id = Guid.NewGuid(), Section = "Summary", Ordinal = 0, Claim = "Build settlements to 10 VP.",
                    Citations = new[] { new MechanicCardCitationSnapshot { PdfId = pdfId, PdfPage = 1, Quote = "first to 10 points wins" } } },
            }
        };

        db.Set<MechanicCardEntity>().Add(new MechanicCardEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            OriginAnalysisId = analysisId,
            Origin = "ai_reviewed",
            Title = "Catan — Comprehension Card",
            Content = content.ToJson(),
            Version = 1,
            IsSuppressed = suppressed,
            SuppressedReason = suppressed ? "takedown request" : null,
            SuppressedAt = suppressed ? DateTime.UtcNow : null,
            SuppressedBy = suppressed ? TestUserId : null,
            ErrorReportsCount = 0,
            FeedbackScore = null,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = TestUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return gameId;
    }
}
