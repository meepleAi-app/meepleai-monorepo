using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for AgentDefinition ChatLanguage property (E5-3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentDefinitionLanguageTests
{
    private static AgentDefinition CreateDefaultAgent()
    {
        var config = AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f);
        return AgentDefinition.Create("TestAgent", "Test description", AgentType.RagAgent, config);
    }

    [Fact]
    public void ChatLanguage_DefaultsToAuto()
    {
        // Act
        var agent = CreateDefaultAgent();

        // Assert
        agent.ChatLanguage.Should().Be("auto");
    }

    [Fact]
    public void SetChatLanguage_Auto_ShouldSetAuto()
    {
        // Arrange
        var agent = CreateDefaultAgent();
        agent.SetChatLanguage("it"); // set to something else first

        // Act
        agent.SetChatLanguage("auto");

        // Assert
        agent.ChatLanguage.Should().Be("auto");
    }

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("ja")]
    [InlineData("zh")]
    public void SetChatLanguage_ValidCode_ShouldSet(string languageCode)
    {
        // Arrange
        var agent = CreateDefaultAgent();

        // Act
        agent.SetChatLanguage(languageCode);

        // Assert
        agent.ChatLanguage.Should().Be(languageCode);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("IT")]
    [InlineData("ita")]
    [InlineData("i")]
    [InlineData("It")]
    [InlineData("123")]
    [InlineData("")]
    public void SetChatLanguage_InvalidCode_ThrowsArgumentException(string invalidCode)
    {
        // Arrange
        var agent = CreateDefaultAgent();

        // Act
        var act = () => agent.SetChatLanguage(invalidCode);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("languageCode");
    }

    [Fact]
    public void SetChatLanguage_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var agent = CreateDefaultAgent();

        // Act
        var act = () => agent.SetChatLanguage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("languageCode");
    }

    [Fact]
    public void SetChatLanguage_ShouldUpdateTimestamp()
    {
        // Arrange
        var agent = CreateDefaultAgent();
        agent.UpdatedAt.Should().BeNull();

        // Act
        agent.SetChatLanguage("it");

        // Assert
        agent.UpdatedAt.Should().NotBeNull();
        agent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
