using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Unit tests for ContentPart, TextContentPart, ImageContentPart, and LlmMessage.
/// Session Vision AI feature.
/// </summary>
public class ContentPartTests
{
    // ─── TextContentPart ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void TextContentPart_StoresText()
    {
        var part = new TextContentPart("Hello world");

        part.Text.Should().Be("Hello world");
    }

    // ─── ImageContentPart ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ImageContentPart_ToDataUri_GeneratesCorrectFormat()
    {
        var part = new ImageContentPart("abc123==", "image/jpeg");

        var uri = part.ToDataUri();

        uri.Should().Be("data:image/jpeg;base64,abc123==");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ImageContentPart_StoresBase64DataAndMediaType()
    {
        var part = new ImageContentPart("data==", "image/png");

        part.Base64Data.Should().Be("data==");
        part.MediaType.Should().Be("image/png");
    }

    // ─── LlmMessage.FromText ────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void FromText_CreatesTextOnlyMessage()
    {
        var message = LlmMessage.FromText("user", "Hello");

        message.Role.Should().Be("user");
        message.Content.Should().HaveCount(1);
        message.Content[0].Should().BeOfType<TextContentPart>()
            .Which.Text.Should().Be("Hello");
    }

    // ─── LlmMessage.HasImages ───────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void HasImages_WithImageContentPart_ReturnsTrue()
    {
        var message = new LlmMessage("user", new ContentPart[]
        {
            new TextContentPart("Look at this:"),
            new ImageContentPart("abc==", "image/jpeg")
        });

        message.HasImages.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void HasImages_WithOnlyTextContentParts_ReturnsFalse()
    {
        var message = LlmMessage.FromText("user", "Just text");

        message.HasImages.Should().BeFalse();
    }
}
