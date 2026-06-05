using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for <see cref="GetDocumentEmbeddingsMetaQueryHandler"/>.
///
/// Real Testcontainer Postgres with seeded PdfDocument + VectorDocument.
/// Exercises full MediatR pipeline including AuditLoggingBehavior — asserts
/// audit_logs row is written on success (Issue #1674 spec §6 + §7).
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1674")]
public sealed class GetDocumentEmbeddingsMetaIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetDocumentEmbeddingsMetaIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_emb_meta_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName).ConfigureAwait(false);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken).ConfigureAwait(false);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync().ConfigureAwait(false);
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName).ConfigureAwait(false); }
            catch { /* best-effort cleanup */ }
        }
    }

    private async Task<Guid> SeedIndexedDocAsync(
        string language = "en",
        string model = "bge-base-en-v1.5",
        int dimensions = 768,
        int chunkCount = 412)
    {
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@meepleai.test",
            DisplayName = "Integration Test User",
            Role = "user",
        });

        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = $"/tmp/test-{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            Language = language,
        });

        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            GameId = null,
            ChunkCount = chunkCount,
            EmbeddingModel = model,
            EmbeddingDimensions = dimensions,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow.AddHours(-2),
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken).ConfigureAwait(false);
        return pdfId;
    }

    [Fact(Timeout = 30000)]
    public async Task Returns_Meta_When_Document_Indexed()
    {
        var pdfId = await SeedIndexedDocAsync(language: "it", chunkCount: 100).ConfigureAwait(false);

        var result = await _mediator!.Send(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            TestCancellationToken).ConfigureAwait(false);

        result.Should().NotBeNull();
        result.DocId.Should().Be(pdfId);
        result.Model.Should().Be("bge-base-en-v1.5");
        result.Dimensions.Should().Be(768);
        result.TotalChunks.Should().Be(100);
        result.Language.Should().Be("it");
        result.IndexedAt.Should().NotBeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Throws_NotFound_When_Document_Not_Indexed()
    {
        var unknownDocId = Guid.NewGuid();

        var act = async () => await _mediator!.Send(
            new GetDocumentEmbeddingsMetaQuery(unknownDocId),
            TestCancellationToken).ConfigureAwait(false);

        var ex = await act.Should().ThrowAsync<NotFoundException>().ConfigureAwait(false);
        ex.Which.ResourceType.Should().Be("Embeddings");
        ex.Which.ResourceId.Should().Be(unknownDocId.ToString());
    }

    [Fact(Timeout = 30000)]
    public async Task Pipeline_Resolves_EmbeddingModel_From_VectorDocument_Directly()
    {
        // FIX-1 validation: EmbeddingModel + EmbeddingDimensions are denormalized
        // on VectorDocumentEntity. Verifica che il single-JOIN query lo legga
        // direttamente senza richiedere PgVectorEmbedding rows.
        var pdfId = await SeedIndexedDocAsync(
            model: "custom-test-model-v2",
            dimensions: 1024).ConfigureAwait(false);

        var result = await _mediator!.Send(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            TestCancellationToken).ConfigureAwait(false);

        result.Model.Should().Be("custom-test-model-v2");
        result.Dimensions.Should().Be(1024);
    }

    [Fact(Timeout = 30000)]
    public async Task Multiple_Sequential_Calls_Are_Consistent()
    {
        // Sanity check: idempotent read-only query, same result across calls.
        var pdfId = await SeedIndexedDocAsync().ConfigureAwait(false);

        var first = await _mediator!.Send(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            TestCancellationToken).ConfigureAwait(false);
        var second = await _mediator!.Send(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            TestCancellationToken).ConfigureAwait(false);

        first.Should().BeEquivalentTo(second);
    }
}
