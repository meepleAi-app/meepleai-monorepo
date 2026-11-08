using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
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
/// La fixture di <see cref="WizardCreateIdempotencyTests"/>.
/// </summary>
/// <remarks>
/// #4050. Host condiviso per la classe, database fresco per test. I doppi di BGG e del
/// downloader delle copertine vivono quanto la classe: nessun test li interroga, servono perche'
/// <c>SharedGame.Create()</c> pretende descrizione e immagini non vuote e perche' il downloader
/// non faccia HTTP vero. Il client resta per test, perche' porta gli header di autenticazione.
/// </remarks>
public sealed class WizardCreateIdempotencyHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "wizard_idempotency_test")
{
    protected override Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ConfigureHost(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Mock BGG API to avoid real calls.
                    // Returns full details so SharedGame.Create() gets non-empty
                    // description/imageUrl/thumbnailUrl (domain validation requires them).
                    services.RemoveAll(typeof(Api.Services.IBggApiService));
                    var mockBggApi = new Mock<Api.Services.IBggApiService>();
                    mockBggApi
                        .Setup(x => x.SearchGamesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(new List<BggSearchResultDto>()));
                    mockBggApi
                        .Setup(x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<BggGameDetailsDto?>(new BggGameDetailsDto(
                            174430,
                            "Test Game BGG",
                            "A test board game description for idempotency integration tests.",
                            2020,
                            2,
                            4,
                            60,
                            30,
                            120,
                            10,
                            7.5,
                            7.0,
                            10000,
                            2.5,
                            "https://example.com/thumbnail.jpg",
                            "https://example.com/image.jpg",
                            new List<string> { "Strategy" },
                            new List<string> { "Worker Placement" },
                            new List<string> { "Test Designer" },
                            new List<string> { "Test Publisher" })));
                    services.AddScoped(_ => mockBggApi.Object);

                    // Mock IBggCoverDownloader to return null (tolerated fallback — no R2 upload).
                    // Without this mock the typed HttpClient tries to make a real HTTP call.
                    services.RemoveAll(typeof(IBggCoverDownloader));
                    var mockCoverDownloader = new Mock<IBggCoverDownloader>();
                    mockCoverDownloader
                        .Setup(x => x.DownloadAndUploadAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<string?>(null));
                    services.AddScoped(_ => mockCoverDownloader.Object);

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
}

/// <summary>
/// Integration tests for G5: Idempotency-Key support on POST /admin/shared-games/wizard/create.
/// Verifies double-submit protection, body-mismatch 422, and different-key independence.
/// Uses in-process IDistributedCache (AddDistributedMemoryCache) via IntegrationWebApplicationFactory
/// with Redis:Enabled=false (default), which is sufficient for same-process idempotency verification.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WizardCreateIdempotencyTests
    : IClassFixture<WizardCreateIdempotencyHostFixture>, IAsyncLifetime
{
    private readonly WizardCreateIdempotencyHostFixture _hostFixture;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Guid _testUserId;

    public WizardCreateIdempotencyTests(WizardCreateIdempotencyHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
        _testUserId = Guid.NewGuid();
    }

    // #4050. Host e doppi vengono dalla fixture; qui restano un database clonato dal template
    // (gia' migrato) e un client nuovo, perche' DefaultRequestHeaders.Add aggiunge e non
    // sostituisce: su un client per classe gli header si accumulerebbero a ogni test.
    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, _testUserId.ToString());
        _client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "Admin");
    }

    // Host e database appartengono alla fixture; il client e' di questa istanza.
    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: seed a minimal UserEntity (once) + PdfDocumentEntity so the
    // handler can resolve UploadedByUserId FK constraint.
    // ─────────────────────────────────────────────────────────────────────────
    private bool _userSeeded;

    private async Task EnsureUserSeededAsync(MeepleAiDbContext db)
    {
        if (_userSeeded) return;
        var existing = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == _testUserId);
        if (!existing)
        {
            db.Users.Add(new UserEntity
            {
                Id = _testUserId,
                Email = $"idempotency-test-{_testUserId:N}@test.local",
                DisplayName = "Idempotency Test Admin",
                PasswordHash = "test-hash",
                Role = "Admin",
                Tier = "free",
                CreatedAt = DateTime.UtcNow,
                EmailVerified = true
            });
            await db.SaveChangesAsync();
        }
        _userSeeded = true;
    }

    private async Task<Guid> SeedOrphanPdfAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await EnsureUserSeededAsync(db);

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
            // SelectedBggId triggers the BGG mock which supplies description/imageUrl/thumbnailUrl
            // required by SharedGame.Create() domain validation.
            SelectedBggId = 174430
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
            MinAge = 10,
            SelectedBggId = 174430
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
            // Use distinct BggIds: shared_games has a unique-per-non-null index on bgg_id.
            SelectedBggId = 174430
        };
        var requestB = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfB,
            ExtractedTitle = titleB,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = 174431
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
