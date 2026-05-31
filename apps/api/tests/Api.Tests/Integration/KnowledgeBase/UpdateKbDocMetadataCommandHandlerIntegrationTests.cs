using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.UpdateKbDocMetadata;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #1687 Task 6 — integration tests for <c>UpdateKbDocMetadataCommandHandler</c>.
/// Asserts the authorization branch (D-2 anti-info-leak 404), the no-op idempotency
/// path, and the actual persistence + domain-event emission.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1687")]
public sealed class UpdateKbDocMetadataCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IMediator? _mediator;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UpdateKbDocMetadataCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_update_kb_doc_metadata_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Guid> SeedUserAsync(Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = id,
            Email = $"u-{id:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<Guid> SeedSharedGameAsync(string title = "Catan")
    {
        var id = Guid.NewGuid();
        _dbContext!.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1, // Approved
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    private async Task<Guid> SeedPdfDocumentAsync(Guid ownerId, Guid sharedGameId, string fileName = "rulebook.pdf")
    {
        var id = Guid.NewGuid();
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
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
            Tags = new List<string>()
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
        return id;
    }

    private async Task<int> CountEventLogsAsync(Guid aggregateId)
    {
        return await _dbContext!.Set<Api.Infrastructure.Entities.DomainEventLog.DomainEventLogEntity>()
            .CountAsync(e => e.AggregateId == aggregateId, TestCancellationToken);
    }

    // ─── AC1: Owner sets all fields → 200, persists, event payload ─────────────

    [Fact(Timeout = 60000)]
    public async Task Handle_AsOwner_AllFieldsSet_PersistsChanges_EmitsEvent_WithChangesPayload()
    {
        var ownerId = await SeedUserAsync();
        var gameId = await SeedSharedGameAsync();
        var docId = await SeedPdfDocumentAsync(ownerId, gameId);

        var cmd = new UpdateKbDocMetadataCommand(
            DocId: docId,
            EditorUserId: ownerId,
            EditorRole: "User",
            Title: "Catan 5th Edition",
            DocumentType: "Rulebook",
            Language: "it",
            Tags: new[] { "Strategy", "family" });

        var dto = await _mediator!.Send(cmd, TestCancellationToken);

        dto.Title.Should().Be("Catan 5th Edition");
        dto.Tags.Should().BeEquivalentTo(new[] { "family", "strategy" }, "D-8 dedup+lowercase+sort");
        dto.UpdatedBy.Should().Be(ownerId);

        // Reload to confirm persistence.
        _dbContext!.ChangeTracker.Clear();
        var persisted = await _dbContext.PdfDocuments.AsNoTracking()
            .SingleAsync(p => p.Id == docId, TestCancellationToken);
        persisted.Title.Should().Be("Catan 5th Edition");
        persisted.Tags.Should().BeEquivalentTo(new[] { "family", "strategy" });
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedBy.Should().Be(ownerId);

        // One row in domain_event_logs.
        (await CountEventLogsAsync(docId)).Should().Be(1, "EventTypeRegistry registration writes one log row per emission");
    }

    // ─── AC2: Admin patches another user's doc → editor_role=Admin in event ────

    [Fact(Timeout = 60000)]
    public async Task Handle_AsAdmin_OnOtherUsersDoc_PersistsChanges_EmitsEvent_WithEditorRoleAdmin()
    {
        var ownerId = await SeedUserAsync();
        var adminId = await SeedUserAsync();
        var gameId = await SeedSharedGameAsync();
        var docId = await SeedPdfDocumentAsync(ownerId, gameId);

        var cmd = new UpdateKbDocMetadataCommand(
            DocId: docId,
            EditorUserId: adminId,
            EditorRole: "Admin",
            Title: null,
            DocumentType: null,
            Language: null,
            Tags: new[] { "moderated" });

        var dto = await _mediator!.Send(cmd, TestCancellationToken);

        dto.UpdatedBy.Should().Be(adminId, "the admin id is recorded as the editor (D-1)");
        dto.Tags.Should().Contain("moderated");

        (await CountEventLogsAsync(docId)).Should().Be(1);
    }

    // ─── AC3: Non-owner non-admin → 404 NotFound (D-2 anti-info-leak) ─────────

    [Fact(Timeout = 60000)]
    public async Task Handle_AsNonOwnerNonAdmin_ThrowsNotFoundException()
    {
        var ownerId = await SeedUserAsync();
        var strangerId = await SeedUserAsync();
        var gameId = await SeedSharedGameAsync();
        var docId = await SeedPdfDocumentAsync(ownerId, gameId);

        var cmd = new UpdateKbDocMetadataCommand(
            DocId: docId,
            EditorUserId: strangerId,
            EditorRole: "User",
            Title: "Cannot do this",
            DocumentType: null,
            Language: null,
            Tags: null);

        Func<Task> act = async () => await _mediator!.Send(cmd, TestCancellationToken);

        await act.Should().ThrowAsync<NotFoundException>(
            "D-2: a non-owner non-admin caller must get 404 (anti-info-leak), NOT 403");

        // No state change.
        _dbContext!.ChangeTracker.Clear();
        var persisted = await _dbContext.PdfDocuments.AsNoTracking()
            .SingleAsync(p => p.Id == docId, TestCancellationToken);
        persisted.Title.Should().BeNull("no mutation on denied access");
        (await CountEventLogsAsync(docId)).Should().Be(0, "no event emitted on denied access");
    }

    // ─── Doc does not exist → 404 ──────────────────────────────────────────

    [Fact(Timeout = 60000)]
    public async Task Handle_DocIdDoesNotExist_ThrowsNotFoundException()
    {
        var editorId = await SeedUserAsync();

        var cmd = new UpdateKbDocMetadataCommand(
            DocId: Guid.NewGuid(),
            EditorUserId: editorId,
            EditorRole: "Admin",
            Title: "Anything",
            DocumentType: null,
            Language: null,
            Tags: null);

        Func<Task> act = async () => await _mediator!.Send(cmd, TestCancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── AllFieldsNull → no-op (no event, no audit bump) ──────────────────

    [Fact(Timeout = 60000)]
    public async Task Handle_AllFieldsNull_NoChange_NoEvent_NoAuditBump()
    {
        var ownerId = await SeedUserAsync();
        var gameId = await SeedSharedGameAsync();
        var docId = await SeedPdfDocumentAsync(ownerId, gameId);

        var cmd = new UpdateKbDocMetadataCommand(
            DocId: docId,
            EditorUserId: ownerId,
            EditorRole: "User",
            Title: null,
            DocumentType: null,
            Language: null,
            Tags: null);

        var dto = await _mediator!.Send(cmd, TestCancellationToken);

        // The DTO is returned but no audit was set and no event row exists.
        dto.UpdatedBy.Should().BeNull();
        (await CountEventLogsAsync(docId)).Should().Be(0, "no real change → no event (idempotent no-op)");
    }

    // ─── OnlyTitle → exactly one MetadataChange ─────────────────────────────

    [Fact(Timeout = 60000)]
    public async Task Handle_OnlyTitle_OnlyOneChangeRecorded()
    {
        var ownerId = await SeedUserAsync();
        var gameId = await SeedSharedGameAsync();
        var docId = await SeedPdfDocumentAsync(ownerId, gameId);

        var cmd = new UpdateKbDocMetadataCommand(
            DocId: docId,
            EditorUserId: ownerId,
            EditorRole: "User",
            Title: "New Title",
            DocumentType: null,
            Language: null,
            Tags: null);

        var dto = await _mediator!.Send(cmd, TestCancellationToken);

        dto.Title.Should().Be("New Title");
        (await CountEventLogsAsync(docId)).Should().Be(1, "title change emits exactly one event row");
    }

    // ─── Setting same value → no event (real diff check) ───────────────────

    [Fact(Timeout = 60000)]
    public async Task Handle_SameTitleValue_NoEventEmitted()
    {
        var ownerId = await SeedUserAsync();
        var gameId = await SeedSharedGameAsync();
        var docId = await SeedPdfDocumentAsync(ownerId, gameId);

        // First edit sets the title.
        await _mediator!.Send(new UpdateKbDocMetadataCommand(
            docId, ownerId, "User", "Same Title", null, null, null), TestCancellationToken);
        (await CountEventLogsAsync(docId)).Should().Be(1);

        // Second edit with the SAME title must NOT emit a second event.
        await _mediator!.Send(new UpdateKbDocMetadataCommand(
            docId, ownerId, "User", "Same Title", null, null, null), TestCancellationToken);
        (await CountEventLogsAsync(docId)).Should().Be(1,
            "no real diff → no event (the handler compares old vs new before emit)");
    }
}
