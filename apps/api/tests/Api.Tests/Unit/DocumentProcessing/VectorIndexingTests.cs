using Api.Infrastructure.Entities.KnowledgeBase;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

public sealed class VectorIndexingTests
{
    [Fact]
    public void PgVectorEmbeddingEntity_CanBeConstructed_WithRequiredFields()
    {
        var entity = new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "test content",
            Vector = new Pgvector.Vector(new float[] { 0.1f, 0.2f, 0.3f }),
            Model = "intfloat/multilingual-e5-base",
            ChunkIndex = 0,
            PageNumber = 1,
            Lang = "it"
        };

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("it", entity.Lang);
        Assert.Equal(0, entity.ChunkIndex);
    }

    [Fact]
    public void PgVectorEmbeddingEntity_Lang_DefaultsToEn()
    {
        var entity = new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "test",
            Vector = new Pgvector.Vector(new float[] { 0.1f }),
            Model = "test-model",
            ChunkIndex = 0,
            PageNumber = 1
        };

        Assert.Equal("en", entity.Lang);
    }
}
