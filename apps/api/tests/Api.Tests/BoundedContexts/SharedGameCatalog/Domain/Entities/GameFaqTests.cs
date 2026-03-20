using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameFaq entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 10
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameFaqTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsGameFaq()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        // Act
        var faq = GameFaq.Create(
            sharedGameId: sharedGameId,
            question: "How do I win the game?",
            answer: "Score the most points by the end of round 10.",
            displayOrder: 0);

        // Assert
        faq.Id.Should().NotBe(Guid.Empty);
        faq.SharedGameId.Should().Be(sharedGameId);
        faq.Question.Should().Be("How do I win the game?");
        faq.Answer.Should().Be("Score the most points by the end of round 10.");
        faq.DisplayOrder.Should().Be(0);
        faq.UpvoteCount.Should().Be(0);
        faq.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        faq.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithHighDisplayOrder_Succeeds()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        // Act
        var faq = GameFaq.Create(sharedGameId, "Question?", "Answer.", displayOrder: 100);

        // Assert
        faq.DisplayOrder.Should().Be(100);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => GameFaq.Create(
            Guid.Empty,
            "Question?",
            "Answer.",
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyQuestion_ThrowsArgumentException(string? question)
    {
        // Act
        var action = () => GameFaq.Create(
            Guid.NewGuid(),
            question!,
            "Answer.",
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Question is required*");
    }

    [Fact]
    public void Create_WithQuestionExceeding500Characters_ThrowsArgumentException()
    {
        // Arrange
        var longQuestion = new string('Q', 501);

        // Act
        var action = () => GameFaq.Create(
            Guid.NewGuid(),
            longQuestion,
            "Answer.",
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Question cannot exceed 500 characters*");
    }

    [Fact]
    public void Create_WithQuestionAt500Characters_Succeeds()
    {
        // Arrange
        var question = new string('Q', 500);

        // Act
        var faq = GameFaq.Create(Guid.NewGuid(), question, "Answer.", 0);

        // Assert
        faq.Question.Should().HaveLength(500);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyAnswer_ThrowsArgumentException(string? answer)
    {
        // Act
        var action = () => GameFaq.Create(
            Guid.NewGuid(),
            "Question?",
            answer!,
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Answer is required*");
    }

    [Fact]
    public void Create_WithNegativeDisplayOrder_ThrowsArgumentException()
    {
        // Act
        var action = () => GameFaq.Create(
            Guid.NewGuid(),
            "Question?",
            "Answer.",
            -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DisplayOrder cannot be negative*");
    }

    #endregion

    #region UpdateDisplayOrder Tests

    [Fact]
    public void UpdateDisplayOrder_WithValidOrder_UpdatesOrderAndTimestamp()
    {
        // Arrange
        var faq = GameFaq.Create(Guid.NewGuid(), "Question?", "Answer.", 0);
        var beforeUpdate = DateTime.UtcNow;

        // Act
        faq.UpdateDisplayOrder(5);

        // Assert
        faq.DisplayOrder.Should().Be(5);
        faq.UpdatedAt.Should().NotBeNull();
        faq.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public void UpdateDisplayOrder_WithNegativeOrder_ThrowsArgumentException()
    {
        // Arrange
        var faq = GameFaq.Create(Guid.NewGuid(), "Question?", "Answer.", 0);

        // Act
        var action = () => faq.UpdateDisplayOrder(-1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DisplayOrder cannot be negative*");
    }

    [Fact]
    public void UpdateDisplayOrder_WithZero_Succeeds()
    {
        // Arrange
        var faq = GameFaq.Create(Guid.NewGuid(), "Question?", "Answer.", 5);

        // Act
        faq.UpdateDisplayOrder(0);

        // Assert
        faq.DisplayOrder.Should().Be(0);
    }

    #endregion

    #region Upvote Tests

    [Fact]
    public void Upvote_IncrementsUpvoteCount()
    {
        // Arrange
        var faq = GameFaq.Create(Guid.NewGuid(), "Question?", "Answer.", 0);

        // Act
        var newCount = faq.Upvote();

        // Assert
        newCount.Should().Be(1);
        faq.UpvoteCount.Should().Be(1);
    }

    [Fact]
    public void Upvote_MultipleTimes_AccumulatesCount()
    {
        // Arrange
        var faq = GameFaq.Create(Guid.NewGuid(), "Question?", "Answer.", 0);

        // Act
        faq.Upvote();
        faq.Upvote();
        var newCount = faq.Upvote();

        // Assert
        newCount.Should().Be(3);
        faq.UpvoteCount.Should().Be(3);
    }

    [Fact]
    public void Upvote_UpdatesTimestamp()
    {
        // Arrange
        var faq = GameFaq.Create(Guid.NewGuid(), "Question?", "Answer.", 0);
        var beforeUpvote = DateTime.UtcNow;

        // Act
        faq.Upvote();

        // Assert
        faq.UpdatedAt.Should().NotBeNull();
        faq.UpdatedAt.Should().BeOnOrAfter(beforeUpvote);
    }

    #endregion
}
