using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class AgentTypeTests
{
    [Fact]
    public void RagAgent_HasCorrectProperties()
    {
        // Act
        var agentType = AgentType.RagAgent;

        // Assert
        agentType.Value.Should().Be("RAG");
        agentType.Description.Should().Be("Retrieval-Augmented Generation for general game rules questions");
        agentType.ToString().Should().Be("RAG");
    }

    [Fact]
    public void CitationAgent_HasCorrectProperties()
    {
        // Act
        var agentType = AgentType.CitationAgent;

        // Assert
        agentType.Value.Should().Be("Citation");
        agentType.Description.Should().Contain("Validates sources");
    }

    [Fact]
    public void ConfidenceAgent_HasCorrectProperties()
    {
        // Act
        var agentType = AgentType.ConfidenceAgent;

        // Assert
        agentType.Value.Should().Be("Confidence");
        agentType.Description.Should().Contain("confidence and quality");
    }

    [Fact]
    public void RulesInterpreter_HasCorrectProperties()
    {
        // Act
        var agentType = AgentType.RulesInterpreter;

        // Assert
        agentType.Value.Should().Be("RulesInterpreter");
        agentType.Description.Should().Contain("game rules interpretation");
    }

    [Fact]
    public void ConversationAgent_HasCorrectProperties()
    {
        // Act
        var agentType = AgentType.ConversationAgent;

        // Assert
        agentType.Value.Should().Be("Conversation");
        agentType.Description.Should().Contain("chat threads");
    }

    [Theory]
    [InlineData("RAG")]
    [InlineData("rag")]
    [InlineData("RaG")]
    public void Parse_ValidRagAgentValue_ReturnsRagAgent(string value)
    {
        // Act
        var agentType = AgentType.Parse(value);

        // Assert
        agentType.Should().Be(AgentType.RagAgent);
    }

    [Theory]
    [InlineData("CITATION")]
    [InlineData("citation")]
    [InlineData("Citation")]
    public void Parse_ValidCitationAgentValue_ReturnsCitationAgent(string value)
    {
        // Act
        var agentType = AgentType.Parse(value);

        // Assert
        agentType.Should().Be(AgentType.CitationAgent);
    }

    [Theory]
    [InlineData("CONFIDENCE")]
    [InlineData("confidence")]
    public void Parse_ValidConfidenceAgentValue_ReturnsConfidenceAgent(string value)
    {
        // Act
        var agentType = AgentType.Parse(value);

        // Assert
        agentType.Should().Be(AgentType.ConfidenceAgent);
    }

    [Theory]
    [InlineData("RULESINTERPRETER")]
    [InlineData("rulesinterpreter")]
    [InlineData("RulesInterpreter")]
    public void Parse_ValidRulesInterpreterValue_ReturnsRulesInterpreter(string value)
    {
        // Act
        var agentType = AgentType.Parse(value);

        // Assert
        agentType.Should().Be(AgentType.RulesInterpreter);
    }

    [Theory]
    [InlineData("CONVERSATION")]
    [InlineData("conversation")]
    public void Parse_ValidConversationAgentValue_ReturnsConversationAgent(string value)
    {
        // Act
        var agentType = AgentType.Parse(value);

        // Assert
        agentType.Should().Be(AgentType.ConversationAgent);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("")]
    [InlineData("Unknown")]
    public void Parse_InvalidValue_ThrowsArgumentException(string invalidValue)
    {
        // Act
        var act = () => AgentType.Parse(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*Unknown AgentType: {invalidValue}*");
    }

    [Fact]
    public void Parse_NullValue_ThrowsArgumentException()
    {
        // Act
        var act = () => AgentType.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_ValidValue_ReturnsTrueAndAgentType()
    {
        // Act
        var result = AgentType.TryParse("RAG", out var agentType);

        // Assert
        result.Should().BeTrue();
        agentType.Should().Be(AgentType.RagAgent);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_InvalidValue_ReturnsFalseAndNull(string? invalidValue)
    {
        // Act
        var result = AgentType.TryParse(invalidValue!, out var agentType);

        // Assert
        result.Should().BeFalse();
        agentType.Should().BeNull();
    }

    [Fact]
    public void Custom_WithValidParameters_CreatesAgentType()
    {
        // Act
        var agentType = AgentType.Custom("CustomAgent", "Custom description");

        // Assert
        agentType.Value.Should().Be("CustomAgent");
        agentType.Description.Should().Be("Custom description");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Custom_WithNullOrEmptyValue_ThrowsArgumentException(string? invalidValue)
    {
        // Act
        var act = () => AgentType.Custom(invalidValue!, "Description");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*AgentType value cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Custom_WithNullOrEmptyDescription_ThrowsArgumentException(string? invalidDescription)
    {
        // Act
        var act = () => AgentType.Custom("Value", invalidDescription!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*AgentType description cannot be empty*");
    }

    // ========================================================================
    // Strategist and Narrator types — Issue #5280
    // ========================================================================

    [Fact]
    public void Strategist_HasCorrectProperties()
    {
        var agentType = AgentType.Strategist;
        agentType.Value.Should().Be("Strategist");
        agentType.Description.Should().Contain("Strategic");
    }

    [Fact]
    public void Narrator_HasCorrectProperties()
    {
        var agentType = AgentType.Narrator;
        agentType.Value.Should().Be("Narrator");
        agentType.Description.Should().Contain("narrative");
    }

    [Theory]
    [InlineData("STRATEGIST")]
    [InlineData("strategist")]
    [InlineData("Strategist")]
    public void Parse_ValidStrategistValue_ReturnsStrategist(string value)
    {
        var agentType = AgentType.Parse(value);
        agentType.Should().Be(AgentType.Strategist);
    }

    [Theory]
    [InlineData("NARRATOR")]
    [InlineData("narrator")]
    public void Parse_ValidNarratorValue_ReturnsNarrator(string value)
    {
        var agentType = AgentType.Parse(value);
        agentType.Should().Be(AgentType.Narrator);
    }

    [Theory]
    [InlineData("DECISORE")]
    [InlineData("decisore")]
    [InlineData("Decisore")]
    public void Parse_DecisoreAlias_ReturnsStrategist(string value)
    {
        var agentType = AgentType.Parse(value);
        agentType.Should().Be(AgentType.Strategist);
    }
}
