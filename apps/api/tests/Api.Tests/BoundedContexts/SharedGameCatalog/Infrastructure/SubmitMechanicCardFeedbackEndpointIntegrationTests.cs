using System.Net;
using System.Net.Http.Json;

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
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test crea il proprio utente
/// (<c>CreateUserSessionAsync(db, Guid.NewGuid())</c>) e la propria card via <c>SeedCardAsync</c>,
/// che genera game/analysis/card con id nuovi. L'unico conteggio, <c>CountFeedbackAsync</c>, e'
/// filtrato per <c>CardId</c>, quindi non vede le righe degli altri test.
/// </para>
/// <para>
/// Il caso delicato e' <c>Submit_ExceedingDailyCap_Returns429</c>: il cap e' <b>per utente</b>, e
/// l'utente e' nuovo a ogni test, quindi il contatore riparte da zero comunque. Se il cap fosse
/// globale la classe fallirebbe il criterio.
/// </para>
/// </summary>
public sealed class SubmitMechanicCardFeedbackHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "submit_card_feedback");

/// <summary>
/// Integration tests for <c>POST /api/v1/mechanic-cards/{cardId}/feedback</c> (#533 ME-M3.1) against
/// real PostgreSQL: create (201), idempotent change (200, no duplicate), per-day cap (429), missing
/// card (404), and the authentication gate (401).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SubmitMechanicCardFeedbackEndpointIntegrationTests
    : IClassFixture<SubmitMechanicCardFeedbackHostFixture>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string _userSessionToken = null!;
    private Guid _userId;

    public SubmitMechanicCardFeedbackEndpointIntegrationTests(SubmitMechanicCardFeedbackHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    // Il seeding resta per test — xUnit crea una nuova istanza della classe per ogni [Fact] — ma
    // costruisce solo l'utente, non l'host. L'id e' nuovo a ogni test, ed e' cio' che tiene
    // isolato il cap giornaliero per utente di Submit_ExceedingDailyCap_Returns429.
    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, token) = await TestSessionHelper.CreateUserSessionAsync(dbContext, Guid.NewGuid());
        _userId = userId;
        _userSessionToken = token;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record FeedbackBody(Guid ClaimId, bool IsPositive, string? ErrorType, string? Description, string? SuggestedCitation);

    private async Task<HttpResponseMessage> PostFeedbackAsync(Guid cardId, FeedbackBody body, bool authenticated)
    {
        if (!authenticated)
        {
            return await _client.PostAsJsonAsync($"/api/v1/mechanic-cards/{cardId}/feedback", body);
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/mechanic-cards/{cardId}/feedback", _userSessionToken);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Submit_NewPositiveFeedback_Returns201_AndPersists()
    {
        var cardId = await SeedCardAsync();
        var claimId = Guid.NewGuid();

        var response = await PostFeedbackAsync(cardId, new FeedbackBody(claimId, true, null, null, null), authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await CountFeedbackAsync(cardId)).Should().Be(1);
    }

    [Fact]
    public async Task Submit_SameClaimTwice_Returns200_AndDoesNotDuplicate()
    {
        var cardId = await SeedCardAsync();
        var claimId = Guid.NewGuid();

        var first = await PostFeedbackAsync(cardId, new FeedbackBody(claimId, true, null, null, null), authenticated: true);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Change 👍 → 👎 with an error report: same (card,user,claim) → UPDATE, not a new row.
        var second = await PostFeedbackAsync(cardId,
            new FeedbackBody(claimId, false, "factual", "Wrong rule", null), authenticated: true);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountFeedbackAsync(cardId)).Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var row = await db.MechanicCardFeedback.AsNoTracking().SingleAsync(f => f.CardId == cardId);
        row.IsPositive.Should().BeFalse();
        row.ErrorType.Should().Be("factual");
    }

    [Fact]
    public async Task Submit_ExceedingDailyCap_Returns429()
    {
        var cardId = await SeedCardAsync();

        // Seed 10 feedback rows today for this user (same card, distinct claims → unique-index safe).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            for (var i = 0; i < 10; i++)
            {
                db.MechanicCardFeedback.Add(new MechanicCardFeedbackEntity
                {
                    Id = Guid.NewGuid(),
                    CardId = cardId,
                    UserId = _userId,
                    ClaimId = Guid.NewGuid(),
                    IsPositive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        // The 11th NEW feedback (a fresh claim) is over the cap.
        var response = await PostFeedbackAsync(cardId, new FeedbackBody(Guid.NewGuid(), true, null, null, null), authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Submit_ForMissingCard_Returns404()
    {
        var response = await PostFeedbackAsync(Guid.NewGuid(), new FeedbackBody(Guid.NewGuid(), true, null, null, null), authenticated: true);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_WithoutSession_Returns401()
    {
        var cardId = await SeedCardAsync();
        var response = await PostFeedbackAsync(cardId, new FeedbackBody(Guid.NewGuid(), true, null, null, null), authenticated: false);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CountFeedbackAsync(Guid cardId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.MechanicCardFeedback.AsNoTracking().CountAsync(f => f.CardId == cardId);
    }

    private async Task<Guid> SeedCardAsync()
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
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow
        });

        var analysisId = Guid.NewGuid();
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = gameId,
            PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "mechanic-extractor-v1",
            Status = (int)Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            TotalTokensUsed = 0,
            EstimatedCostUsd = 0m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        var cardId = Guid.NewGuid();
        db.Set<MechanicCardEntity>().Add(new MechanicCardEntity
        {
            Id = cardId,
            SharedGameId = gameId,
            OriginAnalysisId = analysisId,
            Origin = "ai_reviewed",
            Title = "Catan — Comprehension Card",
            Content = "{}",
            Version = 1,
            IsSuppressed = false,
            ErrorReportsCount = 0,
            FeedbackScore = null,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return cardId;
    }
}
