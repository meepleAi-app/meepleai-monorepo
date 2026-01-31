using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class FAQQuestionTests
{
    [Fact]
    public void Create_ValidQuestion_Succeeds()
    {
        // Arrange
        var question = "What is the FAQ question format?";

        // Act
        var faqQuestion = new FAQQuestion(question);

        // Assert
        faqQuestion.Value.Should().Be(question);
        faqQuestion.ToString().Should().Be(question);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrEmptyQuestion_ThrowsArgumentException(string? invalidQuestion)
    {
        // Act
        var act = () => new FAQQuestion(invalidQuestion!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*FAQ question cannot be empty*");
    }

    [Fact]
    public void Create_QuestionExceeding500Characters_ThrowsArgumentException()
    {
        // Arrange
        var longQuestion = new string('a', 501);

        // Act
        var act = () => new FAQQuestion(longQuestion);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*FAQ question cannot exceed 500 characters*");
    }

    [Fact]
    public void Create_QuestionWithWhitespace_TrimsWhitespace()
    {
        // Arrange
        var questionWithWhitespace = "  How to play?  ";

        // Act
        var faqQuestion = new FAQQuestion(questionWithWhitespace);

        // Assert
        faqQuestion.Value.Should().Be("How to play?");
    }

    [Fact]
    public void Equals_TwoQuestionsWithSameValue_AreEqual()
    {
        // Arrange
        var question1 = new FAQQuestion("Same question");
        var question2 = new FAQQuestion("Same question");

        // Act & Assert
        question1.Should().Be(question2);
        question1.GetHashCode().Should().Be(question2.GetHashCode());
    }

    [Fact]
    public void Equals_TwoQuestionsWithDifferentValues_AreNotEqual()
    {
        // Arrange
        var question1 = new FAQQuestion("Question 1");
        var question2 = new FAQQuestion("Question 2");

        // Act & Assert
        question1.Should().NotBe(question2);
    }
}
