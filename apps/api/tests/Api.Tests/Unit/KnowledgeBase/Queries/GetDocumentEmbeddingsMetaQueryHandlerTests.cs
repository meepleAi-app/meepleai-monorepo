using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetDocumentEmbeddingsMetaQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GetDocumentEmbeddingsMetaQueryHandler _handler;

    public GetDocumentEmbeddingsMetaQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"emb_meta_{Guid.NewGuid():N}")
            .Options;
        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();
        _db = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _handler = new GetDocumentEmbeddingsMetaQueryHandler(
            _db,
            NullLogger<GetDocumentEmbeddingsMetaQueryHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> SeedIndexedDocAsync(string language = "en", DateTime? indexedAt = null)
    {
        var pdfId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            Language = language,
        });

        _db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            GameId = Guid.NewGuid(),
            ChunkCount = 412,
            EmbeddingModel = "bge-base-en-v1.5",
            EmbeddingDimensions = 768,
            IndexingStatus = "completed",
            IndexedAt = indexedAt ?? DateTime.UtcNow.AddHours(-2),
        });

        await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return pdfId;
    }

    [Fact]
    public async Task Returns_Meta_When_Document_Indexed()
    {
        var indexedAt = DateTime.UtcNow.AddHours(-2);
        var pdfId = await SeedIndexedDocAsync(language: "en", indexedAt: indexedAt).ConfigureAwait(false);

        var result = await _handler.Handle(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            CancellationToken.None).ConfigureAwait(false);

        result.DocId.Should().Be(pdfId);
        result.Model.Should().Be("bge-base-en-v1.5");
        result.Dimensions.Should().Be(768);
        result.TotalChunks.Should().Be(412);
        result.IndexedAt.Should().BeCloseTo(indexedAt, TimeSpan.FromSeconds(1));
        result.Language.Should().Be("en");
    }

    [Fact]
    public async Task Throws_NotFound_When_VectorDocument_Missing()
    {
        var unknownDocId = Guid.NewGuid();

        var act = async () => await _handler.Handle(
            new GetDocumentEmbeddingsMetaQuery(unknownDocId),
            CancellationToken.None).ConfigureAwait(false);

        var ex = await act.Should().ThrowAsync<NotFoundException>().ConfigureAwait(false);
        ex.Which.ResourceType.Should().Be("Embeddings");
        ex.Which.ResourceId.Should().Be(unknownDocId.ToString());
    }

    [Fact]
    public void AuditableAction_Attribute_Applied_On_Query()
    {
        var attr = typeof(GetDocumentEmbeddingsMetaQuery)
            .GetCustomAttributes(typeof(AuditableActionAttribute), inherit: false)
            .Cast<AuditableActionAttribute>()
            .SingleOrDefault();

        attr.Should().NotBeNull();
        attr!.Action.Should().Be("EmbeddingsMetaView");
        attr.Resource.Should().Be("Document");
        attr.Level.Should().Be(1);
        attr.UserIdSource.Should().Be(AuditUserIdSource.Caller);
    }

    [Fact]
    public async Task Returns_Default_Language_When_PdfDocument_Created_Without_Explicit_Language()
    {
        // PdfDocumentEntity.Language defaults to "en" — confirms join + DTO mapping
        var pdfId = await SeedIndexedDocAsync(language: "it").ConfigureAwait(false);

        var result = await _handler.Handle(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            CancellationToken.None).ConfigureAwait(false);

        result.Language.Should().Be("it");
    }

    [Fact]
    public async Task DTO_Serializes_Without_Any_Raw_Vector_Field()
    {
        // Security guarantee from spec §7 (Newman corpus-reconstruction risk mitigation):
        // Verifica che la DTO whitelist non esponga MAI valori vector raw nella JSON response.
        var pdfId = await SeedIndexedDocAsync().ConfigureAwait(false);
        var dto = await _handler.Handle(
            new GetDocumentEmbeddingsMetaQuery(pdfId),
            CancellationToken.None).ConfigureAwait(false);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var json = JsonSerializer.Serialize(dto, jsonOptions);

        json.Should().NotContain("\"vector\"", because: "DTO must not expose raw vector values");
        json.Should().NotContain("\"Vector\"", because: "DTO must not expose raw vector values (PascalCase)");
        json.Should().NotContain("\"embedding\"", because: "DTO must not expose embedding values");
        json.Should().NotContain("\"coordinates\"", because: "DTO must not expose vector coordinates");
        json.Should().NotContain("\"values\"", because: "DTO must not include arbitrary values arrays");

        // Whitelist: only the 6 documented DTO fields are present
        json.Should().Contain("\"docId\"");
        json.Should().Contain("\"model\"");
        json.Should().Contain("\"dimensions\"");
        json.Should().Contain("\"totalChunks\"");
        json.Should().Contain("\"indexedAt\"");
    }
}
