using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class FAQAnswerTests
{
    [Fact]
    public void Create_ValidAnswer_Succeeds()
    {
        // Arrange
        var answer = "This is a valid FAQ answer";

        // Act
        var faqAnswer = new FAQAnswer(answer);

        // Assert
        faqAnswer.Value.Should().Be(answer);
        faqAnswer.ToString().Should().Be(answer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrEmptyAnswer_ThrowsArgumentException(string? invalidAnswer)
    {
        // Act
        var act = () => new FAQAnswer(invalidAnswer!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*FAQ answer cannot be empty*");
    }

    [Fact]
    public void Create_AnswerExceeding5000Characters_ThrowsArgumentException()
    {
        // Arrange
        var longAnswer = new string('a', 5001);

        // Act
        var act = () => new FAQAnswer(longAnswer);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*FAQ answer cannot exceed 5000 characters*");
    }

    [Fact]
    public void Create_AnswerWithWhitespace_TrimsWhitespace()
    {
        // Arrange
        var answerWithWhitespace = "  Trimmed answer  ";

        // Act
        var faqAnswer = new FAQAnswer(answerWithWhitespace);

        // Assert
        faqAnswer.Value.Should().Be("Trimmed answer");
    }

    [Fact]
    public void Equals_TwoAnswersWithSameValue_AreEqual()
    {
        // Arrange
        var answer1 = new FAQAnswer("Same answer");
        var answer2 = new FAQAnswer("Same answer");

        // Act & Assert
        answer1.Should().Be(answer2);
        answer1.GetHashCode().Should().Be(answer2.GetHashCode());
    }

    [Fact]
    public void Equals_TwoAnswersWithDifferentValues_AreNotEqual()
    {
        // Arrange
        var answer1 = new FAQAnswer("Answer 1");
        var answer2 = new FAQAnswer("Answer 2");

        // Act & Assert
        answer1.Should().NotBe(answer2);
    }
}
