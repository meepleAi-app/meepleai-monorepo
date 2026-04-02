using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Unit tests for KbSuggestedQuestionsJob static template helper.
/// KB-09: Daily suggested questions batch job.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbSuggestedQuestionsJobTests
{
    [Fact]
    public void GenerateQuestions_ReturnsExactly5Questions()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions("Puerto Rico");

        Assert.Equal(5, questions.Count);
    }

    [Fact]
    public void GenerateQuestions_ContainsGameNameInQuestions()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions("Catan");

        Assert.All(questions, q => Assert.Contains("Catan", q));
    }

    [Fact]
    public void GenerateQuestions_NullGameName_UsesPlaceholder()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions(null);

        Assert.Equal(5, questions.Count);
        Assert.All(questions, q => Assert.Contains("questo gioco", q));
    }

    [Fact]
    public void GenerateQuestions_WhitespaceGameName_UsesPlaceholder()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions("   ");

        Assert.Equal(5, questions.Count);
        Assert.All(questions, q => Assert.Contains("questo gioco", q));
    }
}
