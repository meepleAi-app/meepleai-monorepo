using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.Services;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class TranslatedChunkMapperTests
{
    [Fact]
    public void ForTranslation_PreservesHeadingAndHierarchy()
    {
        var orig = new DocumentChunkInput { Text = "Disponi", Page = 3, CharStart = 10, CharEnd = 17, Heading = "Setup", Level = 2, ElementType = "text" };
        var translated = TranslatedChunkMapper.ForTranslation(orig, "Lay out");
        translated.Text.Should().Be("Lay out");
        translated.Heading.Should().Be("Setup");   // heading inherited on the EN chunk (was dropped before SP2)
        translated.Page.Should().Be(3);
        translated.Level.Should().Be(2);
        translated.ElementType.Should().Be("text");
    }
}
