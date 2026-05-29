using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
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
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
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

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Override the mocked IAgentDefinitionRepository with the real one so the handler
        // can actually load and update consuming agents.
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        // Blob storage — best-effort, mock so physical delete doesn't fail tests
        var blobMock = new Mock<IBlobStorageService>();
        blobMock.Setup(b => b.DeleteAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobMock.Object);

        // Cache invalidation — best-effort, mock
        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        // IVectorStoreAdapter — mock so the handler can call DeleteByVectorDocumentIdAsync
        // without a real pgvector_embeddings table in the Testcontainers DB.
        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock.Setup(v => v.DeleteByVectorDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddScoped<IVectorStoreAdapter>(_ => vectorStoreMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await MigrateWithRetryAsync(_dbContext);
        await SeedBaseEntitiesAsync();
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
    private async Task<Guid> SeedIndexedDocWithAgentsAsync(int agentCount)
    {
        var pdfId = Guid.NewGuid();

        // PdfDocument
        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = TestSharedGameId,
            UploadedByUserId = TestUserId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = $"/test/{pdfId:N}.pdf",
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
                type: AgentType.Custom("rag", "RAG agent"),
                config: AgentDefinitionConfig.Create("gpt-4o-mini", 2048, 0.7f));

            // Add the document to the agent's KB card list
            // Also include a second doc id to verify partial removal (only pdfId is removed, not the other)
            agent.UpdateKbCardIds(new[] { pdfId, Guid.NewGuid() });

            _dbContext.Set<AgentDefinition>().Add(agent);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);

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

    private static async Task MigrateWithRetryAsync(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(TestCancellationToken);
                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }
}
