using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.UserNotifications.Infrastructure;

[Trait("Category", TestCategories.Unit)]
public sealed class EmailTemplateServiceTests
{
    private readonly EmailTemplateService _sut;

    public EmailTemplateServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "http://localhost:3000"
            })
            .Build();

        _sut = new EmailTemplateService(configuration);
    }

    [Fact]
    public void RenderDocumentReady_ContainsRequiredElements()
    {
        var html = _sut.RenderDocumentReady("Test User", "rules.pdf", "/documents/123");

        html.Should().Contain("MeepleAI");
        html.Should().Contain("Test User");
        html.Should().Contain("rules.pdf");
        html.Should().Contain("/documents/123");
        html.Should().Contain("PDF Ready");
        html.Should().Contain("Start Chatting");
        html.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public void RenderDocumentReady_ContainsUnsubscribeLink()
    {
        var html = _sut.RenderDocumentReady("User", "file.pdf", "/doc/1");

        html.Should().Contain("settings/notifications");
        html.Should().Contain("Manage notification preferences");
    }

    [Fact]
    public void RenderDocumentReady_EscapesHtmlInInputs()
    {
        var html = _sut.RenderDocumentReady(
            "<script>alert('xss')</script>",
            "file<>.pdf",
            "/docs/123");

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void RenderDocumentFailed_ContainsErrorDetails()
    {
        var html = _sut.RenderDocumentFailed("User", "rules.pdf", "OCR engine timeout");

        html.Should().Contain("MeepleAI");
        html.Should().Contain("PDF Processing Failed");
        html.Should().Contain("rules.pdf");
        html.Should().Contain("OCR engine timeout");
        html.Should().Contain("Contact Support");
        html.Should().Contain("Common Solutions");
    }

    [Fact]
    public void RenderDocumentFailed_EscapesErrorMessage()
    {
        var html = _sut.RenderDocumentFailed("User", "file.pdf", "<img onerror=alert(1)>");

        html.Should().NotContain("<img onerror");
        html.Should().Contain("&lt;img onerror");
    }

    [Fact]
    public void RenderRetryAvailable_ContainsRetryInfo()
    {
        var html = _sut.RenderRetryAvailable("User", "rules.pdf", 2);

        html.Should().Contain("MeepleAI");
        html.Should().Contain("PDF Processing Retry");
        html.Should().Contain("rules.pdf");
        html.Should().Contain("#2");
        html.Should().Contain("retry attempt");
    }

    [Fact]
    public void RenderRetryAvailable_ContainsDarkModeSupport()
    {
        var html = _sut.RenderRetryAvailable("User", "file.pdf", 1);

        html.Should().Contain("color-scheme");
        html.Should().Contain("light dark");
    }

    [Fact]
    public void AllTemplates_ContainBaseStructure()
    {
        var templates = new[]
        {
            _sut.RenderDocumentReady("U", "f.pdf", "/d/1"),
            _sut.RenderDocumentFailed("U", "f.pdf", "error"),
            _sut.RenderRetryAvailable("U", "f.pdf", 1)
        };

        foreach (var html in templates)
        {
            html.Should().Contain("<!DOCTYPE html>");
            html.Should().Contain("<html>");
            html.Should().Contain("</html>");
            html.Should().Contain("MeepleAI");
            html.Should().Contain("Manage notification preferences");
            html.Should().Contain("All rights reserved");
        }
    }
}
