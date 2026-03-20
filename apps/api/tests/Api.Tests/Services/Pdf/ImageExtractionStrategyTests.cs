using Xunit;
using FluentAssertions;
using Api.Services.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Api.Tests.Constants;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]

public class ImageExtractionStrategyTests
{
    [Fact]
    public void EventOccurred_ShouldNotThrow_WhenDataIsNull()
    {
        var strategy = new ImageExtractionStrategy();
        var ex = Record.Exception(() => strategy.EventOccurred(null!, EventType.RENDER_IMAGE));
        ex.Should().BeNull();
    }

    [Fact]
    public void GetSupportedEvents_Returns_RenderImage()
    {
        var strategy = new ImageExtractionStrategy();
        var events = strategy.GetSupportedEvents();
        events.Should().Contain(EventType.RENDER_IMAGE);
    }
}
