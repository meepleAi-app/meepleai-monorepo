using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the AgentInvocationContext value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 31
/// </summary>
[Trait("Category", "Unit")]
public sealed class AgentInvocationContextTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesContext()
    {
        // Arrange
        var query = "How do I setup the game?";
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(3);

        // Act
        var context = new AgentInvocationContext(query, queryVector, candidateEmbeddings);

        // Assert
        context.InvocationId.Should().NotBe(Guid.Empty);
        context.Query.Should().Be(query);
        context.QueryVector.Should().Be(queryVector);
        context.CandidateEmbeddings.Should().HaveCount(3);
        context.GameId.Should().BeNull();
        context.ChatThreadId.Should().BeNull();
        context.UserId.Should().BeNull();
        context.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_WithAllOptionalParameters_SetsAllProperties()
    {
        // Arrange
        var query = "What are the rules?";
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(2);
        var gameId = Guid.NewGuid();
        var chatThreadId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var context = new AgentInvocationContext(
            query,
            queryVector,
            candidateEmbeddings,
            gameId,
            chatThreadId,
            userId);

        // Assert
        context.Query.Should().Be(query);
        context.GameId.Should().Be(gameId);
        context.ChatThreadId.Should().Be(chatThreadId);
        context.UserId.Should().Be(userId);
    }

    [Fact]
    public void Constructor_TrimsQuery()
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(1);

        // Act
        var context = new AgentInvocationContext(
            "  Question with whitespace  ",
            queryVector,
            candidateEmbeddings);

        // Assert
        context.Query.Should().Be("Question with whitespace");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyQuery_ThrowsArgumentException(string? query)
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(1);

        // Act
        var action = () => new AgentInvocationContext(query!, queryVector, candidateEmbeddings);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Query cannot be empty*")
            .WithParameterName("query");
    }

    [Fact]
    public void Constructor_WithNullQueryVector_ThrowsArgumentNullException()
    {
        // Arrange
        var candidateEmbeddings = CreateTestEmbeddings(1);

        // Act
        var action = () => new AgentInvocationContext("query", null!, candidateEmbeddings);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("queryVector");
    }

    [Fact]
    public void Constructor_WithNullCandidateEmbeddings_ThrowsArgumentNullException()
    {
        // Arrange
        var queryVector = CreateTestVector();

        // Act
        var action = () => new AgentInvocationContext("query", queryVector, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("candidateEmbeddings");
    }

    [Fact]
    public void Constructor_WithEmptyCandidateEmbeddings_Works()
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = new List<Embedding>();

        // Act
        var context = new AgentInvocationContext("query", queryVector, candidateEmbeddings);

        // Assert
        context.CandidateEmbeddings.Should().BeEmpty();
        context.CandidateCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_GeneratesUniqueInvocationId()
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(1);

        // Act
        var context1 = new AgentInvocationContext("query1", queryVector, candidateEmbeddings);
        var context2 = new AgentInvocationContext("query2", queryVector, candidateEmbeddings);

        // Assert
        context1.InvocationId.Should().NotBe(context2.InvocationId);
    }

    #endregion

    #region HasGameContext Tests

    [Fact]
    public void HasGameContext_WithGameId_ReturnsTrue()
    {
        // Arrange
        var context = CreateContextWithGame();

        // Act
        var result = context.HasGameContext;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasGameContext_WithoutGameId_ReturnsFalse()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var result = context.HasGameContext;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasChatContext Tests

    [Fact]
    public void HasChatContext_WithChatThreadId_ReturnsTrue()
    {
        // Arrange
        var context = CreateContextWithChatThread();

        // Act
        var result = context.HasChatContext;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasChatContext_WithoutChatThreadId_ReturnsFalse()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var result = context.HasChatContext;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasUserContext Tests

    [Fact]
    public void HasUserContext_WithUserId_ReturnsTrue()
    {
        // Arrange
        var context = CreateContextWithUser();

        // Act
        var result = context.HasUserContext;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasUserContext_WithoutUserId_ReturnsFalse()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var result = context.HasUserContext;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CandidateCount Tests

    [Fact]
    public void CandidateCount_ReturnsCorrectCount()
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(5);
        var context = new AgentInvocationContext("query", queryVector, candidateEmbeddings);

        // Act
        var result = context.CandidateCount;

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void CandidateCount_WithNoEmbeddings_ReturnsZero()
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = new List<Embedding>();
        var context = new AgentInvocationContext("query", queryVector, candidateEmbeddings);

        // Act
        var result = context.CandidateCount;

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Context Combination Tests

    [Fact]
    public void Context_WithAllContextTypes_ReturnsTrue()
    {
        // Arrange
        var context = new AgentInvocationContext(
            "query",
            CreateTestVector(),
            CreateTestEmbeddings(1),
            gameId: Guid.NewGuid(),
            chatThreadId: Guid.NewGuid(),
            userId: Guid.NewGuid());

        // Act & Assert
        context.HasGameContext.Should().BeTrue();
        context.HasChatContext.Should().BeTrue();
        context.HasUserContext.Should().BeTrue();
    }

    [Fact]
    public void Context_WithNoContextTypes_ReturnsFalse()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act & Assert
        context.HasGameContext.Should().BeFalse();
        context.HasChatContext.Should().BeFalse();
        context.HasUserContext.Should().BeFalse();
    }

    [Fact]
    public void Context_WithPartialContext_ReturnsCorrectValues()
    {
        // Arrange - only game context
        var context = new AgentInvocationContext(
            "query",
            CreateTestVector(),
            CreateTestEmbeddings(1),
            gameId: Guid.NewGuid(),
            chatThreadId: null,
            userId: null);

        // Act & Assert
        context.HasGameContext.Should().BeTrue();
        context.HasChatContext.Should().BeFalse();
        context.HasUserContext.Should().BeFalse();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoContextsWithSameValues_AreNotEqual_DueToUniqueInvocationId()
    {
        // Arrange
        var queryVector = CreateTestVector();
        var candidateEmbeddings = CreateTestEmbeddings(1);

        var context1 = new AgentInvocationContext("same query", queryVector, candidateEmbeddings);
        var context2 = new AgentInvocationContext("same query", queryVector, candidateEmbeddings);

        // Act & Assert - InvocationId is unique per instance, so they won't be equal
        context1.Should().NotBe(context2);
        context1.InvocationId.Should().NotBe(context2.InvocationId);
    }

    #endregion

    #region Helper Methods

    private static Vector CreateTestVector(int dimensions = 768)
    {
        var values = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            values[i] = (float)(i * 0.001);
        }
        return new Vector(values);
    }

    private static List<Embedding> CreateTestEmbeddings(int count)
    {
        var embeddings = new List<Embedding>();
        var vectorDocId = Guid.NewGuid();

        for (int i = 0; i < count; i++)
        {
            var vector = CreateTestVector();
            embeddings.Add(new Embedding(
                Guid.NewGuid(),
                vectorDocId,
                $"Test content {i}",
                vector,
                "nomic-embed-text",
                chunkIndex: i,
                pageNumber: 1));
        }

        return embeddings;
    }

    private static AgentInvocationContext CreateMinimalContext()
    {
        return new AgentInvocationContext(
            "minimal query",
            CreateTestVector(),
            CreateTestEmbeddings(1));
    }

    private static AgentInvocationContext CreateContextWithGame()
    {
        return new AgentInvocationContext(
            "game query",
            CreateTestVector(),
            CreateTestEmbeddings(1),
            gameId: Guid.NewGuid());
    }

    private static AgentInvocationContext CreateContextWithChatThread()
    {
        return new AgentInvocationContext(
            "chat query",
            CreateTestVector(),
            CreateTestEmbeddings(1),
            chatThreadId: Guid.NewGuid());
    }

    private static AgentInvocationContext CreateContextWithUser()
    {
        return new AgentInvocationContext(
            "user query",
            CreateTestVector(),
            CreateTestEmbeddings(1),
            userId: Guid.NewGuid());
    }

    #endregion
}
