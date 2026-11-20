using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for DeleteKbDocumentCommandHandler.
/// Issue #1653: F3-FU-4 — admin delete KB doc with agent-cascade + audit.
///
/// Verifies:
/// 1. Document deleted → TextChunks/VectorDocument cascade-deleted
/// 2. Consuming agents have the document id removed from KbCardIds
/// 3. NotFoundException thrown for missing document
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1653")]
public sealed class DeleteKbDocumentCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;
    private readonly Mock<IHybridCacheService> _hybridCacheMock = new();
    private readonly Mock<IBlobStorageService> _blobMock = new();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Stable IDs for seeded parent entities
    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000000001");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000000001");

    public DeleteKbDocumentCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_deletekbdoc_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _blobMock.Setup(b => b.DeleteAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceProvider = BuildServices().BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await TestMigrationHelper.MigrateWithRetryAsync(_dbContext, TestCancellationToken);
        await SeedBaseEntitiesAsync();
    }

    private ServiceCollection BuildServices()
    {
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Override the mocked IAgentDefinitionRepository with the real one so the handler
        // can actually load and update consuming agents.
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        // Blob storage — best-effort, mock so physical delete doesn't fail tests (default set once, in InitializeAsync)
        services.AddSingleton<IBlobStorageService>(_blobMock.Object);

        // Cache invalidation — best-effort, mock
        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        // #3982 ha dato al gestore anche la cache semantica, e qui non era registrata: i tre test
        // di questa classe fallivano alla costruzione del gestore, prima di provarlo.
        services.AddSingleton(Mock.Of<ISemanticResponseCache>());

        // #4244: la cache dell'elenco «I miei documenti». Registrata per ultima, vince sulla base.
        services.AddSingleton(_hybridCacheMock.Object);

        // IVectorStoreAdapter — mock so the handler can call DeleteByVectorDocumentIdAsync
        // without a real pgvector_embeddings table in the Testcontainers DB.
        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock.Setup(v => v.DeleteByVectorDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddScoped<IVectorStoreAdapter>(_ => vectorStoreMock.Object);

        return services;
    }

    /// <summary>
    /// The services above with <see cref="AuditLoggingBehavior{TRequest,TResponse}"/>, as in the app: the command is
    /// [AtomicAudit], so the handler runs inside a transaction that commits only after the audit row (#5093).
    /// </summary>
    private ServiceProvider BuildAtomicProvider(AuditService? auditOverride = null)
    {
        var services = BuildServices();
        services.AddSingleton(Mock.Of<IHttpContextAccessor>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        if (auditOverride is not null)
        {
            services.Replace(ServiceDescriptor.Scoped<AuditService>(_ => auditOverride));
        }

        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RemovesDoc_ChunksVectors_AndDetachesFromConsumingAgents()
    {
        // Arrange: seed doc with 2 text chunks, a vector document, and 2 agents that consume it
        var pdfId = await SeedIndexedDocWithAgentsAsync(agentCount: 2);

        // Act
        await _mediator!.Send(new DeleteKbDocumentCommand(pdfId), TestCancellationToken);

        // Assert: document removed
        (await _dbContext!.PdfDocuments.FindAsync(pdfId, TestCancellationToken))
            .Should().BeNull("document should be hard-deleted");

        // Assert: text chunks cascade-deleted
        (await _dbContext.TextChunks.CountAsync(c => c.PdfDocumentId == pdfId, TestCancellationToken))
            .Should().Be(0, "text chunks must be cascade-deleted with the document");

        // Assert: vector document cascade-deleted
        (await _dbContext.VectorDocuments.CountAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken))
            .Should().Be(0, "vector document must be cascade-deleted with the document");

        // Assert: agents no longer reference the deleted document
        // IgnoreQueryFilters ensures soft-deleted agents are included in the check
        var agents = await _dbContext.Set<AgentDefinition>()
            .IgnoreQueryFilters()
            .ToListAsync(TestCancellationToken);
        agents.Should().OnlyContain(
            a => !a.KbCardIds.Contains(pdfId),
            "consuming agents must have the document id removed from KbCardIds");
    }

    /// <summary>
    /// #4244: la cache ibrida tiene l'elenco «I miei documenti» di chi li ha caricati, il
    /// documento e i suoi passaggi. Senza questa invalidazione un documento cancellato resta
    /// elencato e leggibile fino alla scadenza. Le etichette sono quelle con cui scrivono
    /// <c>ListUserKbDocsQueryHandler</c>, <c>GetKbDocumentByIdHandler</c> e <c>GetKbChunksHandler</c>.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidatesWhatTheCacheHoldsOfTheDocument()
    {
        var pdfId = await SeedIndexedDocWithAgentsAsync(agentCount: 0);

        await _mediator!.Send(new DeleteKbDocumentCommand(pdfId), TestCancellationToken);

        _hybridCacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{TestUserId}", It.IsAny<CancellationToken>()), Times.Once);
        _hybridCacheMock.Verify(
            c => c.RemoveByTagAsync($"kbDoc:{pdfId:N}", It.IsAny<CancellationToken>()), Times.Once);
        _hybridCacheMock.Verify(
            c => c.RemoveByTagAsync($"kbChunks:{pdfId:N}", It.IsAny<CancellationToken>()), Times.Once);
        // #4626: il feed di Discover (GetRecentKbDocsQueryHandler) è uguale per tutti e porta questa etichetta.
        _hybridCacheMock.Verify(
            c => c.RemoveByTagAsync("recentDocs", It.IsAny<CancellationToken>()), Times.Once);
        // L'elenco di tutti gli altri utenti resta dov'è: l'etichetta per utente basta.
        _hybridCacheMock.Verify(
            c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>#5091: il pdfId faceva da fileId, che è casuale (#2671): il PDF restava nello storage.</summary>
    [Fact]
    [Trait("Issue", "5091")]
    public async Task Handle_DeletesTheBlobUnderTheKeyItsFilePathRecords()
    {
        const string storedFileId = "f1e3d5c7b9a04f2e6d8c0b2a4f6e8d0c";
        var resourceKey = $"shared-game-{TestSharedGameId}";
        var pdfId = await SeedIndexedDocWithAgentsAsync(
            agentCount: 0, filePath: $"pdfs/{resourceKey}/{storedFileId}_rules.pdf");

        await _mediator!.Send(new DeleteKbDocumentCommand(pdfId), TestCancellationToken);

        _blobMock.Verify(
            b => b.DeleteAsync(storedFileId, BlobCategory.Pdf, resourceKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    // #5093: il comando è [AtomicAudit], e il behavior fa il commit solo dopo la riga d'audit. Copertina e file
    // aspettano quel commit: prima partivano dentro la transazione, e un commit fallito riportava la riga senza di loro.
    [Fact]
    public async Task Handle_InTheAtomicAudit_DeletesCoverAndFileOnlyOnceTheDeletionIsCommitted()
    {
        var pdfId = await SeedIndexedDocWithAgentsAsync(
            agentCount: 0, filePath: $"pdfs/shared-game-{TestSharedGameId}/0a1b2c3d4e5f60718293a4b5c6d7e8f9_rules.pdf");
        await SetCoverAsync(pdfId);
        var seenByAnotherConnection = new List<string>();
        void RecordWhetherTheRowIsGone(string delete)
        {
            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var gone = !db.PdfDocuments.AsNoTracking().Any(p => p.Id == pdfId);
            seenByAnotherConnection.Add($"{delete}: {(gone ? "committed" : "still there")}");
        }
        _blobMock.Setup(b => b.DeleteAsync(
                It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => RecordWhetherTheRowIsGone("file"))
            .ReturnsAsync(true);
        _blobMock.Setup(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => RecordWhetherTheRowIsGone("cover"))
            .ReturnsAsync(true);
        await using var atomic = BuildAtomicProvider();

        await atomic.GetRequiredService<IMediator>().Send(new DeleteKbDocumentCommand(pdfId), TestCancellationToken);

        // Another connection sees the row gone only after the commit: both deletes waited for it.
        seenByAnotherConnection.Should().Equal("cover: committed", "file: committed");
    }

    [Fact]
    public async Task Handle_WhenTheAuditWriteFails_KeepsTheRowTheCoverAndTheFile()
    {
        var pdfId = await SeedIndexedDocWithAgentsAsync(
            agentCount: 0, filePath: $"pdfs/shared-game-{TestSharedGameId}/0a1b2c3d4e5f60718293a4b5c6d7e8f9_rules.pdf");
        await SetCoverAsync(pdfId);
        await using var atomic = BuildAtomicProvider(new ThrowingAuditService());

        var act = () => atomic.GetRequiredService<IMediator>().Send(new DeleteKbDocumentCommand(pdfId), TestCancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _dbContext!.ChangeTracker.Clear();
        (await _dbContext.PdfDocuments.AsNoTracking().AnyAsync(p => p.Id == pdfId, TestCancellationToken))
            .Should().BeTrue("the transaction rolled back");
        _blobMock.Verify(b => b.DeleteAsync(
            It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _blobMock.Verify(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task SetCoverAsync(Guid pdfId)
    {
        await _dbContext!.PdfDocuments
            .Where(p => p.Id == pdfId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CoverR2Key, $"covers/pdf/{pdfId:N}"), TestCancellationToken);
    }

    /// <summary>Un documento che non c'è non ha un elenco da invalidare.</summary>
    [Fact]
    public async Task Handle_WhenTheDocIsMissing_InvalidatesNothing()
    {
        var act = async () => await _mediator!.Send(
            new DeleteKbDocumentCommand(Guid.NewGuid()), TestCancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
        _hybridCacheMock.Verify(
            c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Throws_NotFound_WhenDocMissing()
    {
        // Act
        var act = async () => await _mediator!.Send(
            new DeleteKbDocumentCommand(Guid.NewGuid()), TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_RemovesDoc_WithNoConsumingAgents_Succeeds()
    {
        // Arrange: doc with chunks/vector but no agents
        var pdfId = await SeedIndexedDocWithAgentsAsync(agentCount: 0);

        // Act
        await _mediator!.Send(new DeleteKbDocumentCommand(pdfId), TestCancellationToken);

        // Assert
        (await _dbContext!.PdfDocuments.FindAsync(pdfId, TestCancellationToken))
            .Should().BeNull("document should be deleted even with no consuming agents");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Seeding helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a PdfDocument with 2 TextChunks, a VectorDocument, and <paramref name="agentCount"/>
    /// AgentDefinitions that list the document in their KbCardIds.
    /// Returns the new PdfDocument id.
    /// </summary>
    private async Task<Guid> SeedIndexedDocWithAgentsAsync(int agentCount, string? filePath = null)
    {
        var pdfId = Guid.NewGuid();

        // PdfDocument
        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = filePath ?? $"/test/{pdfId:N}.pdf",
            FileSizeBytes = 2048,
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            PageCount = 4
        };
        _dbContext!.PdfDocuments.Add(pdfDoc);

        // TextChunks (2)
        for (var i = 0; i < 2; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                SharedGameId = TestSharedGameId,
                Content = $"Chunk {i} content",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 20,
                CreatedAt = DateTime.UtcNow
            });
        }

        // VectorDocument
        var vectorDocId = Guid.NewGuid();
        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocId,
            PdfDocumentId = pdfId,
            GameId = TestSharedGameId,
            ChunkCount = 2,
            TotalCharacters = 40,
            IndexedAt = DateTime.UtcNow,
            IndexingStatus = "completed",
            EmbeddingModel = "test-model",
            EmbeddingDimensions = 384
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // AgentDefinitions that consume this document
        for (var i = 0; i < agentCount; i++)
        {
            var agent = AgentDefinition.Create(
                name: $"test-agent-{pdfId:N}-{i}",
                description: "Test agent for delete KB doc",
                config: AgentDefinitionConfig.Create("gpt-4o-mini", 2048, 0.7f));

            // Add the document to the agent's KB card list
            // Also include a second doc id to verify partial removal (only pdfId is removed, not the other)
            agent.UpdateKbCardIds(new[] { pdfId, Guid.NewGuid() });

            _dbContext.Set<AgentDefinition>().Add(agent);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Issue #3866: the handler reads the document with the production NoTracking default and
        // then Remove()s what it read. A real request never seeded that row in its own scope, so
        // leaving the seed tracked here is what produces the identity conflict — not the handler.
        _dbContext.ChangeTracker.Clear();

        return pdfId;
    }

    private async Task SeedBaseEntitiesAsync()
    {
        // Idempotent: seed user + shared game only if not already present
        if (!await _dbContext!.Users.AnyAsync(u => u.Id == TestUserId, TestCancellationToken))
        {
            _dbContext.Users.Add(new UserEntity
            {
                Id = TestUserId,
                Email = "test-deletekbdoc@meepleai.dev",
                DisplayName = "TestUser",
                Role = "Editor",
                Tier = "Free",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!await _dbContext.SharedGames.AnyAsync(g => g.Id == TestSharedGameId, TestCancellationToken))
        {
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = TestSharedGameId,
                Title = "Test Game for KB Doc Delete",
                YearPublished = 2024,
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

}
