using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightFallbackMessageProviderTests
{
    private readonly DefaultCopyrightFallbackMessageProvider _provider = new();

    [Fact]
    public void GetMessage_It_ReturnsItalianMessage()
    {
        var msg = _provider.GetMessage("it");

        Assert.Contains("letterale", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("riformulare", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMessage_En_ReturnsEnglishMessage()
    {
        var msg = _provider.GetMessage("en");

        Assert.Contains("verbatim", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rephrasing", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMessage_UnknownLanguage_ReturnsEnglishFallback()
    {
        var msg = _provider.GetMessage("de");

        Assert.Contains("verbatim", msg, StringComparison.OrdinalIgnoreCase);
    }
}
