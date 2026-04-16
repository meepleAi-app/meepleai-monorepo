using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ChatWithSessionAgentCommandHandlerLanguageNormalizationTests
{
    [Theory]
    [InlineData(null, "it")]
    [InlineData("", "it")]
    [InlineData("auto", "it")]
    [InlineData("it", "it")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]           // uppercase normalized to lowercase
    [InlineData("IT", "it")]
    [InlineData("fr", "fr")]           // any 2-char passes through (lowered)
    [InlineData("xyz", "it")]          // 3-char falls through to default
    [InlineData("a", "it")]            // 1-char falls through to default
    // Known quirk: 2-char whitespace passes through as "  " (the switch matches Length == 2
    // before any trim). Considered safe because AgentDefinition.SetChatLanguage validates at
    // write path; NormalizeLanguage is a defensive helper, not a validator.
    [InlineData("  ", "  ")]
    public void NormalizeLanguage_ReturnsExpected(string? input, string expected)
    {
        var result = ChatWithSessionAgentCommandHandler.NormalizeLanguage(input);
        Assert.Equal(expected, result);
    }
}
