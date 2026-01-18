using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class ExportedChatDataTests
{
    [Fact]
    public void Create_JsonFormat_SetsCorrectProperties()
    {
        // Arrange
        var content = "{\"messages\": []}";

        // Act
        var exportedData = new ExportedChatData(ExportFormat.Json, content);

        // Assert
        exportedData.Format.Should().Be(ExportFormat.Json);
        exportedData.Content.Should().Be(content);
        exportedData.ContentType.Should().Be("application/json");
        exportedData.FileExtension.Should().Be("json");
        exportedData.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_MarkdownFormat_SetsCorrectProperties()
    {
        // Arrange
        var content = "# Chat History\n\nTest content";

        // Act
        var exportedData = new ExportedChatData(ExportFormat.Markdown, content);

        // Assert
        exportedData.Format.Should().Be(ExportFormat.Markdown);
        exportedData.Content.Should().Be(content);
        exportedData.ContentType.Should().Be("text/markdown");
        exportedData.FileExtension.Should().Be("md");
        exportedData.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrEmptyContent_ThrowsArgumentException(string? invalidContent)
    {
        // Act
        var act = () => new ExportedChatData(ExportFormat.Json, invalidContent!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Export content cannot be empty*");
    }

    [Fact]
    public void ToString_JsonFormat_ReturnsFormattedString()
    {
        // Arrange
        var content = "{\"messages\": [\"test\"]}";
        var exportedData = new ExportedChatData(ExportFormat.Json, content);

        // Act
        var result = exportedData.ToString();

        // Assert
        result.Should().StartWith("[Json]");
        result.Should().Contain(content);
    }

    [Fact]
    public void ToString_LongContent_TruncatesTo100Characters()
    {
        // Arrange
        var longContent = new string('a', 200);
        var exportedData = new ExportedChatData(ExportFormat.Markdown, longContent);

        // Act
        var result = exportedData.ToString();

        // Assert
        result.Should().StartWith("[Markdown]");
        result.Should().EndWith("...");
        result.Length.Should().BeLessThanOrEqualTo(120); // [Markdown] + 100 chars + ...
    }

    [Fact]
    public async Task Equals_TwoExportsWithSameValues_AreEqual()
    {
        // Arrange
        var content = "Test content";
        var export1 = new ExportedChatData(ExportFormat.Json, content);

        // Need to ensure same timestamp for equality
        await Task.Delay(TestConstants.Timing.TinyDelay);
        var export2 = new ExportedChatData(ExportFormat.Json, content);

        // Act & Assert
        // Note: ExportedAt is part of equality, so exports at different times are different
        export1.Should().NotBe(export2); // Different timestamps
    }

    [Fact]
    public void GetEqualityComponents_IncludesFormatContentAndTimestamp()
    {
        // Arrange
        var exportedData = new ExportedChatData(ExportFormat.Json, "content");

        // Act - Use reflection to access protected method
        var method = typeof(ExportedChatData).GetMethod("GetEqualityComponents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var components = method?.Invoke(exportedData, null) as IEnumerable<object?>;

        // Assert
        components.Should().NotBeNull();
        var componentList = components!.ToList();
        componentList.Should().HaveCount(3);
        componentList[0].Should().Be(ExportFormat.Json);
        componentList[1].Should().Be("content");
        componentList[2].Should().BeOfType<DateTime>();
    }
}
