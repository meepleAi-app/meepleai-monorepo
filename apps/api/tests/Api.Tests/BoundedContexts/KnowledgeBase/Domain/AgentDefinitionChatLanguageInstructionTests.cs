using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for ChatLanguage system prompt injection (E5-3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentDefinitionChatLanguageInstructionTests
{
    [Fact]
    public void GetChatLanguageInstruction_Auto_ReturnsNull()
    {
        // Act
        var result = PlaygroundChatCommandHandler.GetChatLanguageInstruction("auto");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetChatLanguageInstruction_EmptyString_ReturnsNull()
    {
        // Act
        var result = PlaygroundChatCommandHandler.GetChatLanguageInstruction("");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("it", "Italian")]
    [InlineData("en", "English")]
    [InlineData("de", "German")]
    [InlineData("fr", "French")]
    [InlineData("es", "Spanish")]
    [InlineData("ja", "Japanese")]
    public void GetChatLanguageInstruction_KnownLanguage_ContainsLanguageName(string code, string expectedName)
    {
        // Act
        var result = PlaygroundChatCommandHandler.GetChatLanguageInstruction(code);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain($"Always respond in {expectedName}");
        result.Should().Contain("translate the relevant section");
    }

    [Fact]
    public void GetChatLanguageInstruction_UnknownLanguage_FallsBackToUpperCase()
    {
        // Act
        var result = PlaygroundChatCommandHandler.GetChatLanguageInstruction("sv");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Always respond in SV");
    }
}
