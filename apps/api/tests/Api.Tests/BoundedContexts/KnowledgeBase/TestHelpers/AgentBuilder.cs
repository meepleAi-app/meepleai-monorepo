using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

/// <summary>
/// Builder for creating Agent test instances with sensible defaults.
/// </summary>
internal class AgentBuilder
{
    private Guid _id = Guid.NewGuid();
    private AgentType _agentType = AgentType.RagAgent;  // Changed from QA
    private string _name = "Test Agent";
    private AgentStrategy _strategy = AgentStrategy.HybridSearch(topK: 10, minScore: 0.70);  // Changed from object initializer
    private bool _isActive = true;

    public AgentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AgentBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public AgentBuilder WithType(AgentType agentType)
    {
        _agentType = agentType;
        return this;
    }

    public AgentBuilder WithStrategy(AgentStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public AgentBuilder AsQA()
    {
        _agentType = AgentType.RagAgent;  // Changed from QA
        _name = "QA Agent";
        return this;
    }

    public AgentBuilder AsExplain()
    {
        _agentType = AgentType.ConversationAgent;  // Changed from Explain
        _name = "Explain Agent";
        return this;
    }

    public AgentBuilder AsSetup()
    {
        _agentType = AgentType.RulesInterpreter;  // Changed from Setup
        _name = "Setup Agent";
        return this;
    }

    public AgentBuilder WithCustomStrategy(double minScore, int topK = 10)
    {
        _strategy = AgentStrategy.HybridSearch(topK: topK, minScore: minScore);  // Changed from object initializer
        return this;
    }

    public AgentBuilder ThatIsActive()
    {
        _isActive = true;
        return this;
    }

    public AgentBuilder ThatIsInactive()
    {
        _isActive = false;
        return this;
    }

    /// <summary>
    /// Builds the Agent instance.
    /// </summary>
    public Agent Build()
    {
        return new Agent(_id, _name, _agentType, _strategy, _isActive);
    }

    /// <summary>
    /// Implicit conversion to Agent for convenience.
    /// </summary>
    public static implicit operator Agent(AgentBuilder builder) => builder.Build();
}

