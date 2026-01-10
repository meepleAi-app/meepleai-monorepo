using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class ExportFormatTests
{
    [Fact]
    public void ExportFormat_HasJsonValue()
    {
        // Act
        var format = ExportFormat.Json;

        // Assert
        format.Should().Be(ExportFormat.Json);
        ((int)format).Should().Be(0);
    }

    [Fact]
    public void ExportFormat_HasMarkdownValue()
    {
        // Act
        var format = ExportFormat.Markdown;

        // Assert
        format.Should().Be(ExportFormat.Markdown);
        ((int)format).Should().Be(1);
    }

    [Fact]
    public void ExportFormat_ToString_ReturnsEnumName()
    {
        // Arrange
        var jsonFormat = ExportFormat.Json;
        var markdownFormat = ExportFormat.Markdown;

        // Act & Assert
        jsonFormat.ToString().Should().Be("Json");
        markdownFormat.ToString().Should().Be("Markdown");
    }

    [Theory]
    [InlineData(0, "Json")]
    [InlineData(1, "Markdown")]
    public void ExportFormat_ParseFromString_WorksCorrectly(int expectedValue, string value)
    {
        // Act
        var parsed = Enum.Parse<ExportFormat>(value);

        // Assert
        ((int)parsed).Should().Be(expectedValue);
    }

    [Fact]
    public void ExportFormat_GetValues_ReturnsAllFormats()
    {
        // Act
        var values = Enum.GetValues<ExportFormat>();

        // Assert
        values.Should().HaveCount(2);
        values.Should().Contain(ExportFormat.Json);
        values.Should().Contain(ExportFormat.Markdown);
    }
}
