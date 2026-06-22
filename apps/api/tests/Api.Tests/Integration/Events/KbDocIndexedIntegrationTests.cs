using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

/// <summary>
/// Integration test: <c>TransitionTo(Ready)</c> on <c>PdfDocument</c> emits exactly ONE
/// <c>kb.doc.indexed</c> row in <c>domain_event_logs</c> — NOT one per transition.
///
/// BE-3 #1590 — design decision B2: only the Ready terminal transition raises the
/// user-meaningful "doc indexed" event. <c>PdfStateChangedEvent</c> (internal, unregistered)
/// continues to fire on every transition for cache/metrics handlers; only
/// <c>KbDocIndexedEvent</c> (registered alias "kb.doc.indexed") reaches the log table.
///
/// Design decision (gameName resolution): <c>PdfDocument.TransitionTo</c> is a pure domain
/// method with no repository access. The event carries <c>GameName = null</c>; the Task 8
/// activity endpoint resolves the name via <c>SharedGameRepository.GetNamesByIds</c>.
/// <c>FileName</c> is the primary rail title.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class KbDocIndexedIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _webFactory = null!;

    public KbDocIndexedIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"kb_doc_indexed_events_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _webFactory = IntegrationWebApplicationFactory.Create(connectionString);
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_webFactory is not null)
            await _webFactory.DisposeAsync();
        if (!string.IsNullOrEmpty(_testDbName))
            await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    /// <summary>
    /// Verifies that exactly ONE <c>kb.doc.indexed</c> row is written when a
    /// <c>PdfDocument</c> transitions through the full pipeline to <c>Ready</c>,
    /// even though <c>PdfStateChangedEvent</c> fires on every intermediate step.
    ///
    /// Pipeline path: Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready.
    /// We call <c>UpdateAsync</c> + <c>SaveChangesAsync</c> only ONCE (after the Ready
    /// transition) to reflect the real production flow where the processing worker
    /// transitions the aggregate and persists at the terminal state.
    /// </summary>
    [Fact]
    public async Task TransitionToReady_LogsKbDocIndexedEvent_ExactlyOnce_NotPerTransition()
    {
        // Arrange: seed user + shared game via TestSessionHelper
        using var arrangeScope = _webFactory.Services.CreateScope();
        var dbContext = arrangeScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, title: "Catan-BE3-Kb");

        // Build PdfDocument domain object (constructor → Pending state, 0 domain events)
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocument(
            id: pdfId,
            gameId: gameId,                             // mirrors into SharedGameId in ctor
            fileName: new FileName($"test-{pdfId:N}.pdf"),
            filePath: $"/uploads/test-{pdfId:N}.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: userId
        );

        // Persist the new aggregate (collects constructor domain events; SaveChanges writes them).
        var repo = arrangeScope.ServiceProvider.GetRequiredService<IPdfDocumentRepository>();
        var unitOfWork = arrangeScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repo.AddAsync(pdf);
        await unitOfWork.SaveChangesAsync();

        // Act: drive the full pipeline to Ready in a NEW scope (mimics processing worker DI scope).
        // Only one UpdateAsync+SaveChanges after the terminal Ready transition.
        using var actScope = _webFactory.Services.CreateScope();
        var actDb = actScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var actRepo = actScope.ServiceProvider.GetRequiredService<IPdfDocumentRepository>();
        var actUow = actScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Re-load the domain object (must re-fetch since DomainEvents live on the in-memory instance)
        var domainPdf = await actRepo.GetByIdAsync(pdfId);
        domainPdf.Should().NotBeNull("the PDF was just persisted in the Arrange scope");

        // Full pipeline: Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready
        // (ValidateStateTransition enforces forward-only; each step emits PdfStateChangedEvent)
        domainPdf!.TransitionTo(PdfProcessingState.Uploading);
        domainPdf.TransitionTo(PdfProcessingState.Extracting);
        domainPdf.TransitionTo(PdfProcessingState.Chunking);
        domainPdf.TransitionTo(PdfProcessingState.Embedding);
        domainPdf.TransitionTo(PdfProcessingState.Indexing);
        domainPdf.TransitionTo(PdfProcessingState.Ready);   // ← ONLY this raises KbDocIndexedEvent

        // Persist: CollectEventsFrom(domainPdf) → MeepleAiDbContext.SaveChangesAsync
        // writes aggregate + domain_event_log rows atomically.
        await actRepo.UpdateAsync(domainPdf);
        await actUow.SaveChangesAsync();

        // Assert: fresh scope to avoid first-level-cache hits
        using var verifyScope = _webFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var rows = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "kb.doc.indexed" && e.AggregateId == pdfId)
            .ToListAsync();

        // Exactly ONE row — not 6 (one per transition) or 0 (event not emitted)
        rows.Should().HaveCount(1,
            "kb.doc.indexed fires only on the Ready transition (B2 decision, #1590)");

        var row = rows[0];

        // AggregateType — DomainEventLogMapper strips "Event": KbDocIndexedEvent → "KbDocIndexed"
        row.AggregateType.Should().Be("KbDocIndexed");

        // UserId — reflected from event.UserId property
        row.UserId.Should().Be(userId);

        // PayloadVersion — v1 schema (BE-3 #1590)
        row.PayloadVersion.Should().Be(1);

        // PayloadJson — DomainEventLogMapper uses JsonNamingPolicy.CamelCase
        // FileName is the PRIMARY title (domain method has no repo access → gameName=null)
        row.PayloadJson.Should().Contain("\"fileName\"",
            "fileName is the primary kb.doc.indexed rail title");
        row.PayloadJson.Should().Contain("\"gameId\"",
            "gameId must be serialized camelCase for the activity endpoint join");
        row.PayloadJson.Should().Contain(gameId.ToString(),
            "gameId value must match the seeded shared game");

        // Confirm PdfStateChangedEvent rows are NOT in kb.doc.indexed
        // (PdfStateChangedEvent is unregistered → only dispatched via MediatR in-memory)
        rows.Should().HaveCount(1,
            "PdfStateChangedEvent (internal, unregistered) must not produce kb.doc.indexed rows");
    }
}
