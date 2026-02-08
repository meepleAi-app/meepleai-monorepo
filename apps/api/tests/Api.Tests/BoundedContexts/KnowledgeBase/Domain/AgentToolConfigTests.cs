using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for AgentToolConfig value object (Issue #3808)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentToolConfigTests
{
    [Fact]
    public void Create_WithValidName_ShouldCreateTool()
    {
        // Act
        var tool = AgentToolConfig.Create("web_search");

        // Assert
        tool.Should().NotBeNull();
        tool.Name.Should().Be("web_search");
        tool.GetSettings().Should().BeEmpty();
    }

    [Fact]
    public void Create_WithSettings_ShouldStoreSettings()
    {
        // Arrange
        var settings = new Dictionary<string, object>
        {
            ["max_results"] = 10,
            ["timeout"] = 30
        };

        // Act
        var tool = AgentToolConfig.Create("web_search", settings);

        // Assert
        tool.GetSettings().Should().ContainKey("max_results");
        tool.GetSettings()["max_results"].Should().BeEquivalentTo(10);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldThrowArgumentException(string name)
    {
        // Act
        var act = () => AgentToolConfig.Create(name!);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithTooLongName_ShouldThrowArgumentException()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act
        var act = () => AgentToolConfig.Create(longName);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void GetSettings_WithNoSettings_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var tool = AgentToolConfig.Create("test_tool");

        // Act
        var settings = tool.GetSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.Should().BeEmpty();
    }
}
