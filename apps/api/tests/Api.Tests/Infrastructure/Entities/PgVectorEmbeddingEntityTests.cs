using Api.Infrastructure.Entities.KnowledgeBase;
using Pgvector;
using Xunit;

namespace Api.Tests.Infrastructure.Entities;

/// <summary>
/// Unit tests for PgVectorEmbeddingEntity.
/// Verifies all required properties exist, types are correct, and defaults are set properly.
/// </summary>
public sealed class PgVectorEmbeddingEntityTests
{
    [Fact]
    public void Entity_HasId_Property()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Entity_HasVectorDocumentId_Property()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(Guid.Empty, entity.VectorDocumentId);
    }

    [Fact]
    public void Entity_HasGameId_Property()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(Guid.Empty, entity.GameId);
    }

    [Fact]
    public void Entity_TextContent_DefaultsToEmptyString()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(string.Empty, entity.TextContent);
    }

    [Fact]
    public void Entity_Vector_IsPgvectorVectorType()
    {
        var floats = new float[768];
        var vector = new Vector(floats);
        var entity = new PgVectorEmbeddingEntity { Vector = vector };

        Assert.IsType<Vector>(entity.Vector);
        Assert.Equal(vector, entity.Vector);
    }

    [Fact]
    public void Entity_Model_DefaultsToEmptyString()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(string.Empty, entity.Model);
    }

    [Fact]
    public void Entity_ChunkIndex_DefaultsToZero()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(0, entity.ChunkIndex);
    }

    [Fact]
    public void Entity_PageNumber_DefaultsToZero()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal(0, entity.PageNumber);
    }

    [Fact]
    public void Entity_CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var entity = new PgVectorEmbeddingEntity();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(entity.CreatedAt, before, after);
    }

    [Fact]
    public void Entity_Lang_DefaultsToEn()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Equal("en", entity.Lang);
    }

    [Fact]
    public void Entity_SourceChunkId_DefaultsToNull()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.Null(entity.SourceChunkId);
    }

    [Fact]
    public void Entity_IsTranslation_DefaultsToFalse()
    {
        var entity = new PgVectorEmbeddingEntity();
        Assert.False(entity.IsTranslation);
    }

    [Fact]
    public void Entity_AllProperties_CanBeSetAndRead()
    {
        var id = Guid.NewGuid();
        var vectorDocumentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sourceChunkId = Guid.NewGuid();
        var floats = Enumerable.Repeat(0.1f, 768).ToArray();
        var vector = new Vector(floats);
        var now = DateTimeOffset.UtcNow;

        var entity = new PgVectorEmbeddingEntity
        {
            Id = id,
            VectorDocumentId = vectorDocumentId,
            GameId = gameId,
            TextContent = "some text content",
            Vector = vector,
            Model = "sentence-transformers/all-MiniLM-L6-v2",
            ChunkIndex = 3,
            PageNumber = 5,
            CreatedAt = now,
            Lang = "it",
            SourceChunkId = sourceChunkId,
            IsTranslation = true
        };

        Assert.Equal(id, entity.Id);
        Assert.Equal(vectorDocumentId, entity.VectorDocumentId);
        Assert.Equal(gameId, entity.GameId);
        Assert.Equal("some text content", entity.TextContent);
        Assert.Equal(vector, entity.Vector);
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", entity.Model);
        Assert.Equal(3, entity.ChunkIndex);
        Assert.Equal(5, entity.PageNumber);
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal("it", entity.Lang);
        Assert.Equal(sourceChunkId, entity.SourceChunkId);
        Assert.True(entity.IsTranslation);
    }

    [Fact]
    public void Entity_DoesNotHaveSearchVector_Property()
    {
        // search_vector is a generated column and must NOT be mapped in EF Core.
        // This test verifies the property does not exist on the entity.
        var propertyInfo = typeof(PgVectorEmbeddingEntity).GetProperty("SearchVector");
        Assert.Null(propertyInfo);
    }
}
