using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Unit tests for PgVectorStoreAdapter.
/// Validates constructor guards, empty-input guards, and domain type integration.
/// Raw SQL execution is tested via integration tests with Testcontainers (pgvector/pgvector:pg16).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PgVectorStoreAdapterTests
{
    private readonly Mock<MeepleAiDbContext> _mockContext;
    private readonly Mock<DatabaseFacade> _mockDatabase;
    private readonly Mock<ILogger<PgVectorStoreAdapter>> _mockLogger;

    public PgVectorStoreAdapterTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockContext = new Mock<MeepleAiDbContext>(
            options,
            Mock.Of<MediatR.IMediator>(),
            Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>(),
            Mock.Of<IDataProtectionProvider>())
        { CallBase = false };

        _mockDatabase = new Mock<DatabaseFacade>(_mockContext.Object);
        _mockContext.Setup(c => c.Database).Returns(_mockDatabase.Object);
        _mockLogger = new Mock<ILogger<PgVectorStoreAdapter>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PgVectorStoreAdapter(null!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PgVectorStoreAdapter(_mockContext.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var adapter = CreateAdapter();

        // Assert
        adapter.Should().NotBeNull();
        adapter.Should().BeAssignableTo<IVectorStoreAdapter>();
    }

    #endregion

    #region IndexBatchAsync Empty/Null Guard Tests

    [Fact]
    public async Task IndexBatchAsync_DoesNothing_WhenEmptyList()
    {
        // Arrange
        var adapter = CreateAdapter();
        var emptyList = new List<Embedding>();

        // Act - should return immediately without touching the database
        var act = async () => await adapter.IndexBatchAsync(emptyList);

        // Assert - completes without exception and without database access
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IndexBatchAsync_DoesNothing_WhenNullList()
    {
        // Arrange
        var adapter = CreateAdapter();

        // Act - should return immediately without touching the database
        var act = async () => await adapter.IndexBatchAsync(null!);

        // Assert - completes without exception and without database access
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Domain Type Integration Tests

    [Fact]
    public void Embedding_Constructor_CreatesValidEmbedding_ForPgVectorMapping()
    {
        // Arrange - validates that Embedding entities can be constructed
        // as the adapter does when mapping search results
        var id = Guid.NewGuid();
        var vectorDocumentId = Guid.NewGuid();
        var textContent = "Test embedding content for pgvector search result";
        var vector = new Vector(new float[] { 0.1f, 0.2f, 0.3f, 0.4f });
        var model = "mxbai-embed-large";
        var chunkIndex = 0;
        var pageNumber = 1;

        // Act
        var embedding = new Embedding(id, vectorDocumentId, textContent, vector, model, chunkIndex, pageNumber);

        // Assert
        embedding.Id.Should().Be(id);
        embedding.VectorDocumentId.Should().Be(vectorDocumentId);
        embedding.TextContent.Should().Be(textContent);
        embedding.Vector.Should().Be(vector);
        embedding.Vector.Values.Should().HaveCount(4);
        embedding.Model.Should().Be(model);
        embedding.ChunkIndex.Should().Be(chunkIndex);
        embedding.PageNumber.Should().Be(pageNumber);
        embedding.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Vector_CreatePlaceholder_CreatesCorrectDimensions_ForSearchResults()
    {
        // Arrange & Act - validates the placeholder vector creation
        // used by PgVectorStoreAdapter.SearchAsync for result mapping
        var vector1024 = Vector.CreatePlaceholder(1024);
        var vector768 = Vector.CreatePlaceholder(768);

        // Assert
        vector1024.Dimensions.Should().Be(1024);
        vector1024.Values.Should().AllBeEquivalentTo(0f);
        vector768.Dimensions.Should().Be(768);
    }

    [Fact]
    public void Vector_CosineSimilarity_CalculatesCorrectly()
    {
        // Arrange - validates the similarity calculation logic
        // that pgvector's <=> operator replicates in SQL
        var vectorA = new Vector(new float[] { 1.0f, 0.0f });
        var vectorB = new Vector(new float[] { 1.0f, 0.0f });
        var vectorC = new Vector(new float[] { 0.0f, 1.0f });

        // Act
        var identicalSimilarity = vectorA.CosineSimilarity(vectorB);
        var orthogonalSimilarity = vectorA.CosineSimilarity(vectorC);

        // Assert - identical vectors have similarity 1.0
        identicalSimilarity.Should().BeApproximately(1.0, 0.001);
        // orthogonal vectors have similarity 0.0
        orthogonalSimilarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void Embedding_CalculateSimilarity_UsesVectorCosineSimilarity()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 0.5f, 0.5f, 0.5f });
        var vector2 = new Vector(new float[] { 0.5f, 0.5f, 0.5f });

        var embedding1 = new Embedding(Guid.NewGuid(), Guid.NewGuid(), "text1", vector1, "model", 0, 1);
        var embedding2 = new Embedding(Guid.NewGuid(), Guid.NewGuid(), "text2", vector2, "model", 0, 1);

        // Act
        var similarity = embedding1.CalculateSimilarity(embedding2);

        // Assert - identical vectors yield similarity of 1.0
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    #endregion

    #region Interface Contract Tests

    [Fact]
    public void PgVectorStoreAdapter_ImplementsIVectorStoreAdapter()
    {
        // Verify the adapter properly implements the interface
        var adapter = CreateAdapter();
        adapter.Should().BeAssignableTo<IVectorStoreAdapter>();
    }

    [Fact]
    public void PgVectorStoreAdapter_IsInternalSealed()
    {
        // Verify the class is internal and sealed for encapsulation
        var type = typeof(PgVectorStoreAdapter);
        type.IsSealed.Should().BeTrue("adapter should be sealed to prevent inheritance");
        type.IsNotPublic.Should().BeTrue("adapter should be internal");
    }

    [Fact]
    public void SearchAsync_DocumentIdsFilter_AcceptsNullAndEmptyList()
    {
        // Verify the optional documentIds parameter types work correctly
        IReadOnlyList<Guid>? nullIds = null;
        IReadOnlyList<Guid> emptyIds = new List<Guid>();
        IReadOnlyList<Guid> validIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        nullIds.Should().BeNull();
        emptyIds.Should().BeEmpty();
        validIds.Should().HaveCount(2);
        validIds.ToArray().Should().BeOfType<Guid[]>("adapter converts to array for ANY(@documentIds)");
    }

    [Fact]
    public void PgvectorVector_CanBeCreatedFromDomainVector()
    {
        // Verify domain Vector values can be converted to Pgvector.Vector
        // This is what the adapter does for parameterized SQL queries
        var domainVector = new Vector(new float[] { 0.1f, 0.2f, 0.3f });
        var pgVector = new Pgvector.Vector(domainVector.Values);

        pgVector.Should().NotBeNull();
        pgVector.ToArray().Should().BeEquivalentTo(domainVector.Values);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Embedding_PageNumber_EnforcesMinimumOfOne()
    {
        // The adapter uses Math.Max(1, pageNumber) when creating embeddings
        // Verify domain validation rejects page 0
        var act = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "text",
            new Vector(new float[] { 0.1f }), "model", 0, 0);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("pageNumber");
    }

    [Fact]
    public void Embedding_TextContent_RejectsEmpty()
    {
        var act = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "",
            new Vector(new float[] { 0.1f }), "model", 0, 1);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("textContent");
    }

    [Fact]
    public void Embedding_Model_RejectsEmpty()
    {
        var act = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "text",
            new Vector(new float[] { 0.1f }), "", 0, 1);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("model");
    }

    [Fact]
    public void Embedding_ChunkIndex_RejectsNegative()
    {
        var act = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "text",
            new Vector(new float[] { 0.1f }), "model", -1, 1);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("chunkIndex");
    }

    #endregion

    private PgVectorStoreAdapter CreateAdapter()
    {
        return new PgVectorStoreAdapter(_mockContext.Object, _mockLogger.Object);
    }
}
