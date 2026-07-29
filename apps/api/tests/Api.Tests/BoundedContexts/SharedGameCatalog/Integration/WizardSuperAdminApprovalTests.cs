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
/// Issue #3367: a SuperAdmin creating a game via the PDF wizard must auto-publish
/// (ApprovalStatus == "Published"), exactly like an Admin — not fall through to the
/// Editor "requires approval" branch.
///
/// The defect: <c>SharedGameCatalogWizardEndpoints.HandleWizardCreateGame</c> derived
/// <c>requiresApproval = !context.User.IsInRole("Admin")</c>. A SuperAdmin's single
/// role claim is normalized to "SuperAdmin" (never "Admin"), so <c>IsInRole("Admin")</c>
/// returned false and the game was created as "Draft". Fix: use
/// <c>context.User.IsAdmin()</c> (Admin || SuperAdmin), mirroring
/// <c>SharedGameCatalogAdminEndpoints</c> (Issue #2845).
///
/// Setup mirrors <see cref="WizardCreateIdempotencyTests"/>: real mediator + handler,
/// BGG mocked, seeded User+Pdf. The role is driven per-request via the
/// <see cref="TestAuthenticationHandler"/> role header, whose claim value ("SuperAdmin")
/// reproduces the production NormalizeRoleClaim semantics the defect depends on.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WizardSuperAdminApprovalTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _testUserId;

    public WizardSuperAdminApprovalTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"wizard_superadmin_approval_{Guid.NewGuid():N}";
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
                    // Mock BGG API so SharedGame.Create() receives non-empty
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
                            "A test board game description for the SuperAdmin approval integration test.",
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

                    // Mock IBggCoverDownloader → null (tolerated fallback, no real HTTP/R2 call).
                    services.RemoveAll(typeof(IBggCoverDownloader));
                    var mockCoverDownloader = new Mock<IBggCoverDownloader>();
                    mockCoverDownloader
                        .Setup(x => x.DownloadAndUploadAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<string?>(null));
                    services.AddScoped(_ => mockCoverDownloader.Object);

                    // Real auth is bypassed with a test scheme; the role is supplied per-request
                    // via the role header so each test can act as a different role.
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

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
        // Only the user id is a client-wide default; the role is set per request.
        _client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, _testUserId.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private bool _userSeeded;

    private async Task EnsureUserSeededAsync(MeepleAiDbContext db)
    {
        if (_userSeeded) return;
        var existing = await db.Users.AsNoTracking().AnyAsync(u => u.Id == _testUserId);
        if (!existing)
        {
            db.Users.Add(new UserEntity
            {
                Id = _testUserId,
                Email = $"wizard-superadmin-{_testUserId:N}@test.local",
                DisplayName = "Wizard SuperAdmin Test",
                PasswordHash = "test-hash",
                Role = "SuperAdmin",
                Tier = "free",
                CreatedAt = DateTime.UtcNow,
                EmailVerified = true
            });
            await db.SaveChangesAsync();
        }
        _userSeeded = true;
    }

    private async Task<Guid> SeedPdfAsync()
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

    private async Task<CreateGameFromPdfResult> CreateGameAsRoleAsync(string role, Guid pdfId)
    {
        var request = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfId,
            ExtractedTitle = $"SuperAdmin Approval Game {Guid.NewGuid():N}",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = 174430,
            // Intentionally set true: the endpoint MUST ignore the DTO value and derive
            // approval from the caller's role. If honored, a SuperAdmin would (wrongly) draft.
            RequiresApproval = true
        };

        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/shared-games/wizard/create")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(TestAuthenticationHandler.RoleHeader, role);

        var response = await _client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the wizard create endpoint returns 201 for an authorized admin-level role");

        var result = await response.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        result.Should().NotBeNull();
        return result!;
    }

    [Fact]
    public async Task WizardCreate_AsSuperAdmin_PublishesImmediately()
    {
        // Arrange
        var pdfId = await SeedPdfAsync();

        // Act
        var result = await CreateGameAsRoleAsync("SuperAdmin", pdfId);

        // Assert — SuperAdmin auto-publishes like Admin (Issue #3367)
        result.ApprovalStatus.Should().Be("Published",
            "a SuperAdmin has full admin privileges and must auto-publish, not create a Draft");
    }

    [Fact]
    public async Task WizardCreate_AsAdmin_PublishesImmediately()
    {
        // Regression guard: the fix must not change existing Admin behavior.
        var pdfId = await SeedPdfAsync();

        var result = await CreateGameAsRoleAsync("Admin", pdfId);

        result.ApprovalStatus.Should().Be("Published",
            "an Admin auto-publishes (baseline behavior preserved by the fix)");
    }
}
