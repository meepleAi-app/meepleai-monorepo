using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;
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

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #1687 Task 9 — HTTP-level integration tests for
/// <c>PATCH /api/v1/kb-docs/{id}</c>. Covers the 8 G/W/T scenarios from D-16
/// of the spec panel:
/// 1. Owner sets title only → 200 + DTO + 1 event row.
/// 2. Admin patches another user's tags → 200 + event with admin id.
/// 3. Non-owner non-admin → 404 (D-2 anti-info-leak), no state change, no event.
/// 4. Unauthenticated → 401.
/// 5. Invalid documentType ("FAQ") → 422, no event.
/// 6. All fields null → 200, no change, no event, audit unchanged.
/// 7. 21 tags → 422, no event.
/// 8. Cached list → PATCH → fresh GET returns updated data (cache invalidation).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1687")]
public sealed class UpdateKbDocMetadataEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UpdateKbDocMetadataEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"test_patch_kb_docs_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync(TestCancellationToken);
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<Guid> SeedSharedGameAsync(MeepleAiDbContext db, string title)
    {
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, title);
        return gameId;
    }

    private async Task<Guid> SeedPdfDocumentAsync(
        MeepleAiDbContext db,
        Guid ownerId,
        Guid sharedGameId,
        string fileName = "rulebook.pdf",
        string? title = null,
        List<string>? tags = null)
    {
        var id = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = fileName,
            FilePath = $"/tmp/{fileName}",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = ownerId,
            UploadedAt = DateTime.UtcNow.AddDays(-1),
            SharedGameId = sharedGameId,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow.AddHours(-12),
            Title = title,
            Tags = tags ?? new List<string>()
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<int> CountEventLogsAsync(MeepleAiDbContext db, Guid aggregateId)
    {
        return await db.Set<Api.Infrastructure.Entities.DomainEventLog.DomainEventLogEntity>()
            .CountAsync(e => e.AggregateId == aggregateId, TestCancellationToken);
    }

    // ─── AC1: Owner patches title → 200 + DTO + event ─────────────────────

    [Fact(Timeout = 60000)]
    public async Task AC1_Owner_PatchesTitle_Returns200_PersistsDtoUpdatesAuditEmitsEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (ownerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            sessionToken,
            new { title = "Catan 5th Edition" });

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserKbDocDto>(TestCancellationToken);
        dto.Should().NotBeNull();
        dto!.Title.Should().Be("Catan 5th Edition");
        dto.UpdatedBy.Should().Be(ownerId);
        (await CountEventLogsAsync(db, docId)).Should().Be(1);
    }

    // ─── AC2: Admin patches another user's tags → 200 + event ──────────────

    [Fact(Timeout = 60000)]
    public async Task AC2_Admin_PatchesTagsOnOtherDoc_Returns200_EditorRoleAdminInPayload()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (ownerId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var (adminId, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            adminToken,
            new { tags = new[] { "Moderated" } });

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserKbDocDto>(TestCancellationToken);
        dto!.UpdatedBy.Should().Be(adminId, "the admin id is recorded as editor (D-1)");
        dto.Tags.Should().BeEquivalentTo(new[] { "moderated" }, "D-8 lowercases on persist");

        (await CountEventLogsAsync(db, docId)).Should().Be(1);
    }

    // ─── AC3: Non-owner non-admin → 404 (D-2 anti-info-leak) ──────────────

    [Fact(Timeout = 60000)]
    public async Task AC3_NonOwnerNonAdmin_Patches_Returns404_NoMutationNoEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (ownerId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            strangerToken,
            new { title = "Should not work" });

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "D-2: never reveal existence of inaccessible docs (no 403)");

        // No state change.
        db.ChangeTracker.Clear();
        var persisted = await db.PdfDocuments.AsNoTracking()
            .SingleAsync(p => p.Id == docId, TestCancellationToken);
        persisted.Title.Should().BeNull("no mutation on denied access");
        (await CountEventLogsAsync(db, docId)).Should().Be(0);
    }

    // ─── AC4: Unauthenticated → 401 ─────────────────────────────────────────

    [Fact(Timeout = 60000)]
    public async Task AC4_Unauthenticated_Patches_Returns401()
    {
        var docId = Guid.NewGuid();

        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/kb-docs/{docId}",
            new { title = "Anything" },
            TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── AC5: Invalid documentType ("FAQ") → 422 ────────────────────────────

    [Fact(Timeout = 60000)]
    public async Task AC5_InvalidDocumentType_FAQ_Returns422_NoEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (ownerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            sessionToken,
            new { documentType = "FAQ" });

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "FluentValidation rejects → ApiExceptionHandlerMiddleware returns 422 (NOT 400)");
        (await CountEventLogsAsync(db, docId)).Should().Be(0);
    }

    // ─── AC6: All fields null → 200 + no event + audit unchanged ────────────

    [Fact(Timeout = 60000)]
    public async Task AC6_AllFieldsNull_Returns200_NoEvent_AuditUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (ownerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            sessionToken,
            new { title = (string?)null, documentType = (string?)null, language = (string?)null, tags = (string[]?)null });

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "all-null body is a no-op, NOT a validation error");
        (await CountEventLogsAsync(db, docId)).Should().Be(0);

        db.ChangeTracker.Clear();
        var persisted = await db.PdfDocuments.AsNoTracking()
            .SingleAsync(p => p.Id == docId, TestCancellationToken);
        persisted.UpdatedAt.Should().BeNull("audit unchanged on no-op");
        persisted.UpdatedBy.Should().BeNull();
    }

    // ─── AC7: 21 tags → 422 ─────────────────────────────────────────────────

    [Fact(Timeout = 60000)]
    public async Task AC7_TwentyOneTags_Returns422_NoEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (ownerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId);

        var tags = Enumerable.Range(0, 21).Select(i => $"tag-{i}").ToArray();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            sessionToken,
            new { tags });

        var response = await _client.SendAsync(request, TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CountEventLogsAsync(db, docId)).Should().Be(0);
    }

    // ─── AC8: Cached list → PATCH → fresh GET shows update ─────────────────

    [Fact(Timeout = 60000)]
    public async Task AC8_CachedListThenPatch_GetReturnsFreshData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (ownerId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: TestCancellationToken);
        var gameId = await SeedSharedGameAsync(db, "Catan");
        var docId = await SeedPdfDocumentAsync(db, ownerId, gameId, title: "Original Title");

        // 1. Warm the cache.
        var firstGet = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/kb-docs", sessionToken);
        var firstResp = await _client.SendAsync(firstGet, TestCancellationToken);
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPayload = await firstResp.Content.ReadFromJsonAsync<KbDocsListResponse>(TestCancellationToken);
        firstPayload!.Items.Single().Title.Should().Be("Original Title");

        // 2. PATCH the title.
        var patch = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Patch,
            $"/api/v1/kb-docs/{docId}",
            sessionToken,
            new { title = "Updated Title" });
        (await _client.SendAsync(patch, TestCancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Fresh GET — must reflect the update (proves cache invalidation handler wired).
        var secondGet = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/kb-docs", sessionToken);
        var secondResp = await _client.SendAsync(secondGet, TestCancellationToken);
        var secondPayload = await secondResp.Content.ReadFromJsonAsync<KbDocsListResponse>(TestCancellationToken);
        secondPayload!.Items.Single().Title.Should().Be("Updated Title",
            "PdfMetadataChangedCacheInvalidationHandler removes the user:{userId} tag so the next GET re-runs the query");
    }
}
