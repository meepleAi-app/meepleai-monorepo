using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for G5: Idempotency-Key support on POST /admin/shared-games/wizard/create.
/// Verifies double-submit protection, body-mismatch 422, and different-key independence.
/// Uses in-process IDistributedCache (AddDistributedMemoryCache) via IntegrationWebApplicationFactory
/// with Redis:Enabled=false (default), which is sufficient for same-process idempotency verification.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WizardCreateIdempotencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _testUserId;

    public WizardCreateIdempotencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"wizard_idempotency_test_{Guid.NewGuid():N}";
        _testUserId = Guid.NewGuid();
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Mock BGG API to avoid real calls
                    services.RemoveAll(typeof(Api.Services.IBggApiService));
                    var mockBggApi = new Mock<Api.Services.IBggApiService>();
                    mockBggApi
                        .Setup(x => x.SearchGamesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(new List<BggSearchResultDto>()));
                    mockBggApi
                        .Setup(x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<BggGameDetailsDto?>(null));
                    services.AddScoped(_ => mockBggApi.Object);

                    // Allow all auth for test purposes
                    services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName, _ => { });

                    var allowAllPolicy = new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes(TestAuthenticationHandler.SchemeName)
                        .RequireAssertion(_ => true)
                        .Build();
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = allowAllPolicy;
                        options.AddPolicy("AdminOrEditorPolicy", allowAllPolicy);
                        options.AddPolicy("AdminOnlyPolicy", allowAllPolicy);
                    });
                });
            });

        // Migrate DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, _testUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "Admin");
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: seed a minimal PdfDocumentEntity so the handler can find the PDF
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<Guid> SeedOrphanPdfAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = $"/tmp/test-{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _testUserId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready"
        });
        await db.SaveChangesAsync();

        return pdfId;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: count SharedGames by extracted title (for duplicate-game assertion)
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<int> CountGamesByTitleAsync(string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.SharedGames
            .AsNoTracking()
            .CountAsync(g => g.Title == title);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: POST to wizard/create with Idempotency-Key header
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<HttpResponseMessage> PostWithIdempotencyKeyAsync(
        CreateGameFromPdfRequest request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/shared-games/wizard/create")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DoubleSubmit_WithSameIdempotencyKey_ReturnsSameGameId()
    {
        // Arrange — seed a PDF the handler can find
        var pdfDocumentId = await SeedOrphanPdfAsync();

        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfDocumentId,
            ExtractedTitle = $"Idempotency Test Game {Guid.NewGuid():N}",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = null
        };

        // Act — first submit
        var firstResponse = await PostWithIdempotencyKeyAsync(request, idempotencyKey);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "first submission with a valid PDF should create a game");

        var firstResult = await firstResponse.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        firstResult.Should().NotBeNull();

        // Act — second submit with identical body + same key
        var secondResponse = await PostWithIdempotencyKeyAsync(request, idempotencyKey);

        // Assert — HTTP 201, same gameId
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "idempotent replay should return 201 from cache");

        var secondResult = await secondResponse.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        secondResult.Should().NotBeNull();
        secondResult!.GameId.Should().Be(firstResult!.GameId,
            "second submit with same Idempotency-Key should return the cached gameId");

        // Assert — only 1 game created in DB
        var gameCount = await CountGamesByTitleAsync(request.ExtractedTitle);
        gameCount.Should().Be(1, "idempotent replay must not create a second game");
    }

    [Fact]
    public async Task SameIdempotencyKey_WithDifferentBody_Returns422()
    {
        // F2 (review finding): IETF draft-ietf-httpapi-idempotency-key §2.6
        // Arrange
        var pdfA = await SeedOrphanPdfAsync();
        var pdfB = await SeedOrphanPdfAsync();

        var key = Guid.NewGuid().ToString();
        var requestA = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfA,
            ExtractedTitle = $"Original Game {Guid.NewGuid():N}",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10
        };
        var requestB_differentBody = requestA with
        {
            PdfDocumentId = pdfB,
            ExtractedTitle = "Completely Different Title"
        };

        // Act — first submit succeeds
        var first = await PostWithIdempotencyKeyAsync(requestA, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created,
            "first submission with valid PDF should succeed");

        // Act — second submit with same key but different body
        var second = await PostWithIdempotencyKeyAsync(requestB_differentBody, key);

        // Assert — 422 Unprocessable Entity per IETF spec §2.6
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "reusing an Idempotency-Key with a different request body must return HTTP 422");
    }

    [Fact]
    public async Task DoubleSubmit_WithDifferentIdempotencyKeys_CreatesTwoGames()
    {
        // Arrange — each key is independent
        var pdfA = await SeedOrphanPdfAsync();
        var pdfB = await SeedOrphanPdfAsync();

        var titleA = $"Game Alpha {Guid.NewGuid():N}";
        var titleB = $"Game Beta {Guid.NewGuid():N}";

        var requestA = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfA,
            ExtractedTitle = titleA,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = null
        };
        var requestB = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfB,
            ExtractedTitle = titleB,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = null
        };

        // Act — two independent keys → two independent creates
        var responseA = await PostWithIdempotencyKeyAsync(requestA, Guid.NewGuid().ToString());
        var responseB = await PostWithIdempotencyKeyAsync(requestB, Guid.NewGuid().ToString());

        // Assert — both 201 and distinct gameIds
        responseA.StatusCode.Should().Be(HttpStatusCode.Created);
        responseB.StatusCode.Should().Be(HttpStatusCode.Created);

        var resultA = await responseA.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        var resultB = await responseB.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();

        resultA.Should().NotBeNull();
        resultB.Should().NotBeNull();
        resultA!.GameId.Should().NotBe(resultB!.GameId,
            "different Idempotency-Keys should produce distinct games");

        // Both games exist in DB
        var countA = await CountGamesByTitleAsync(titleA);
        var countB = await CountGamesByTitleAsync(titleB);
        countA.Should().Be(1);
        countB.Should().Be(1);
    }
}
