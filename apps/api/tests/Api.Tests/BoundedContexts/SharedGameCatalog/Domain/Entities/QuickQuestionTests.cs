using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the QuickQuestion entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 10
/// </summary>
[Trait("Category", "Unit")]
public sealed class QuickQuestionTests
{
    #region CreateFromAI Tests

    [Fact]
    public void CreateFromAI_WithValidData_ReturnsQuickQuestion()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        // Act
        var question = QuickQuestion.CreateFromAI(
            sharedGameId: sharedGameId,
            text: "How do I win?",
            category: QuestionCategory.Winning,
            displayOrder: 0);

        // Assert
        question.Id.Should().NotBe(Guid.Empty);
        question.SharedGameId.Should().Be(sharedGameId);
        question.Text.Should().Be("How do I win?");
        question.Category.Should().Be(QuestionCategory.Winning);
        question.DisplayOrder.Should().Be(0);
        question.IsGenerated.Should().BeTrue();
        question.IsActive.Should().BeTrue();
        question.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(QuestionCategory.Gameplay, "🎮")]
    [InlineData(QuestionCategory.Rules, "📖")]
    [InlineData(QuestionCategory.Winning, "🏆")]
    [InlineData(QuestionCategory.Setup, "⚙️")]
    [InlineData(QuestionCategory.Strategy, "💡")]
    [InlineData(QuestionCategory.Clarifications, "❓")]
    public void CreateFromAI_SetsCorrectEmojiForCategory(QuestionCategory category, string expectedEmoji)
    {
        // Act
        var question = QuickQuestion.CreateFromAI(
            Guid.NewGuid(),
            "Test question",
            category,
            0);

        // Assert
        question.Emoji.Should().Be(expectedEmoji);
    }

    [Fact]
    public void CreateFromAI_TrimsText()
    {
        // Act
        var question = QuickQuestion.CreateFromAI(
            Guid.NewGuid(),
            "  How do I win?  ",
            QuestionCategory.Winning,
            0);

        // Assert
        question.Text.Should().Be("How do I win?");
    }

    #endregion

    #region CreateManual Tests

    [Fact]
    public void CreateManual_WithValidData_ReturnsQuickQuestion()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        // Act
        var question = QuickQuestion.CreateManual(
            sharedGameId: sharedGameId,
            text: "Custom question?",
            emoji: "🔥",
            category: QuestionCategory.Strategy,
            displayOrder: 5);

        // Assert
        question.SharedGameId.Should().Be(sharedGameId);
        question.Text.Should().Be("Custom question?");
        question.Emoji.Should().Be("🔥");
        question.Category.Should().Be(QuestionCategory.Strategy);
        question.IsGenerated.Should().BeFalse();
        question.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateManual_TrimsText()
    {
        // Act - Note: emoji validation happens before trimming, so pass valid emoji
        var question = QuickQuestion.CreateManual(
            Guid.NewGuid(),
            "  Question  ",
            "🎯",
            QuestionCategory.Rules,
            0);

        // Assert
        question.Text.Should().Be("Question");
        question.Emoji.Should().Be("🎯");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void CreateFromAI_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => QuickQuestion.CreateFromAI(
            Guid.Empty,
            "Question?",
            QuestionCategory.Rules,
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromAI_WithEmptyText_ThrowsArgumentException(string? text)
    {
        // Act
        var action = () => QuickQuestion.CreateFromAI(
            Guid.NewGuid(),
            text!,
            QuestionCategory.Rules,
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Question text cannot be empty*");
    }

    [Fact]
    public void CreateFromAI_WithTextExceeding200Characters_ThrowsArgumentException()
    {
        // Arrange
        var longText = new string('Q', 201);

        // Act
        var action = () => QuickQuestion.CreateFromAI(
            Guid.NewGuid(),
            longText,
            QuestionCategory.Rules,
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Question text cannot exceed 200 characters*");
    }

    [Fact]
    public void CreateFromAI_WithTextAt200Characters_Succeeds()
    {
        // Arrange
        var text = new string('Q', 200);

        // Act
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), text, QuestionCategory.Rules, 0);

        // Assert
        question.Text.Should().HaveLength(200);
    }

    [Fact]
    public void CreateFromAI_WithNegativeDisplayOrder_ThrowsArgumentException()
    {
        // Act
        var action = () => QuickQuestion.CreateFromAI(
            Guid.NewGuid(),
            "Question?",
            QuestionCategory.Rules,
            -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Display order cannot be negative*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateManual_WithEmptyEmoji_ThrowsArgumentException(string? emoji)
    {
        // Act
        var action = () => QuickQuestion.CreateManual(
            Guid.NewGuid(),
            "Question?",
            emoji!,
            QuestionCategory.Rules,
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Emoji cannot be empty*");
    }

    [Fact]
    public void CreateManual_WithEmojiExceeding2Characters_ThrowsArgumentException()
    {
        // Act
        var action = () => QuickQuestion.CreateManual(
            Guid.NewGuid(),
            "Question?",
            "🔥🎯❓",
            QuestionCategory.Rules,
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Emoji cannot exceed 2 characters*");
    }

    #endregion

    #region Update Methods Tests

    [Fact]
    public void UpdateText_WithValidText_UpdatesText()
    {
        // Arrange
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), "Original", QuestionCategory.Rules, 0);

        // Act
        question.UpdateText("Updated question?");

        // Assert
        question.Text.Should().Be("Updated question?");
    }

    [Fact]
    public void UpdateText_TrimsText()
    {
        // Arrange
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), "Original", QuestionCategory.Rules, 0);

        // Act
        question.UpdateText("  Updated  ");

        // Assert
        question.Text.Should().Be("Updated");
    }

    [Fact]
    public void UpdateEmoji_WithValidEmoji_UpdatesEmoji()
    {
        // Arrange
        var question = QuickQuestion.CreateManual(Guid.NewGuid(), "Q?", "🎮", QuestionCategory.Rules, 0);

        // Act
        question.UpdateEmoji("🔥");

        // Assert
        question.Emoji.Should().Be("🔥");
    }

    [Fact]
    public void UpdateCategory_UpdatesCategoryAndEmoji()
    {
        // Arrange
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), "Q?", QuestionCategory.Rules, 0);
        question.Emoji.Should().Be("📖"); // Rules emoji

        // Act
        question.UpdateCategory(QuestionCategory.Winning);

        // Assert
        question.Category.Should().Be(QuestionCategory.Winning);
        question.Emoji.Should().Be("🏆"); // Winning emoji
    }

    [Fact]
    public void UpdateDisplayOrder_WithValidOrder_UpdatesOrder()
    {
        // Arrange
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), "Q?", QuestionCategory.Rules, 0);

        // Act
        question.UpdateDisplayOrder(10);

        // Assert
        question.DisplayOrder.Should().Be(10);
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), "Q?", QuestionCategory.Rules, 0);
        question.IsActive.Should().BeTrue();

        // Act
        question.Deactivate();

        // Assert
        question.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveToTrue()
    {
        // Arrange
        var question = QuickQuestion.CreateFromAI(Guid.NewGuid(), "Q?", QuestionCategory.Rules, 0);
        question.Deactivate();

        // Act
        question.Activate();

        // Assert
        question.IsActive.Should().BeTrue();
    }

    #endregion
}