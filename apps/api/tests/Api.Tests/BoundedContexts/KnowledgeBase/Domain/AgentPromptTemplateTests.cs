using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for AgentPromptTemplate value object (Issue #3808)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentPromptTemplateTests
{
    [Theory]
    [InlineData("system", "You are a helpful assistant")]
    [InlineData("user", "User prompt template")]
    [InlineData("assistant", "Assistant response template")]
    public void Create_WithValidData_ShouldCreatePrompt(string role, string content)
    {
        // Act
        var prompt = AgentPromptTemplate.Create(role, content);

        // Assert
        prompt.Should().NotBeNull();
        prompt.Role.Should().Be(role.ToLowerInvariant());
        prompt.Content.Should().Be(content);
    }

    [Theory]
    [InlineData("", "Content")]
    [InlineData(null, "Content")]
    [InlineData("   ", "Content")]
    public void Create_WithInvalidRole_ShouldThrowArgumentException(string role, string content)
    {
        // Act
        var act = () => AgentPromptTemplate.Create(role!, content);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("role");
    }

    [Theory]
    [InlineData("system", "")]
    [InlineData("system", null)]
    [InlineData("system", "   ")]
    public void Create_WithInvalidContent_ShouldThrowArgumentException(string role, string content)
    {
        // Act
        var act = () => AgentPromptTemplate.Create(role, content!);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Theory]
    [InlineData("invalid_role")]
    [InlineData("admin")]
    public void Create_WithUnsupportedRole_ShouldThrowArgumentException(string role)
    {
        // Act
        var act = () => AgentPromptTemplate.Create(role, "Content");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*system, user, assistant, function*");
    }

    [Fact]
    public void Create_WithTooLongContent_ShouldThrowArgumentException()
    {
        // Arrange
        var longContent = new string('a', 10001);

        // Act
        var act = () => AgentPromptTemplate.Create("system", longContent);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }
}
