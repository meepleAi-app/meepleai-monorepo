using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Endpoints;

/// <summary>
/// HTTP-layer IDOR test for <c>POST /api/v1/game-sessions</c>.
/// The endpoint must derive the session owner from the authenticated principal,
/// NOT from a client-supplied body <c>userId</c>. An attacker who sends a victim's
/// id in the request body must still own the created session themselves — otherwise
/// any authenticated user could create sessions owned by (and consume the concurrent-
/// session quota of) an arbitrary victim (owner spoofing / availability DoS).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CreateSessionEndpointIdorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _attackerClient = null!;
    private Guid _attackerId;
    private string _attackerToken = null!;
    private Guid _victimId;
    private Guid _gameId;

    public CreateSessionEndpointIdorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"createsession_idor_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        (_attackerId, _attackerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_victimId, _) = await TestSessionHelper.CreateUserSessionAsync(db);

        // Seed a KB-ready SharedGame so the create-session KB-readiness gate (422 when not
        // ready) passes: a Ready PDF + a completed vector index for that PDF.
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "IDOR Create Game");
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = _gameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = _attackerId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            IsActiveForRag = true,
        });
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            SharedGameId = _gameId,
            IndexingStatus = "completed",
            ChunkCount = 1,
            IndexedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        _attackerClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _attackerClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact(Timeout = 90_000)]
    public async Task CreateSession_WithSpoofedBodyUserId_OwnsSessionAsAuthenticatedCaller()
    {
        // IDOR exploit: the attacker passes the victim's id as the body userId.
        var body = new
        {
            userId = _victimId,
            gameId = _gameId,
            sessionType = "Generic",
            participants = new[] { new { displayName = "Attacker", isOwner = true } },
        };

        var response = await _attackerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/game-sessions", _attackerToken, body));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "a KB-ready game session creation must succeed");

        var result = await response.Content.ReadFromJsonAsync<CreateSessionResult>();
        result.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var session = await db.SessionTrackingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == result!.SessionId);

        session.Should().NotBeNull();
        session!.UserId.Should().Be(
            _attackerId,
            "the session owner must derive from the authenticated principal, not the client-supplied body userId");
    }
}
