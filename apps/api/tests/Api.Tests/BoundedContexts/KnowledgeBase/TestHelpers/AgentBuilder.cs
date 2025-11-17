using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

/// <summary>
/// Builder for creating Agent test instances with sensible defaults.
/// </summary>
public class AgentBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "TestAgent";
    private AgentType _type = AgentType.QA;
    private AgentStrategy _strategy = new AgentStrategy
    {
        SystemPrompt = "You are a helpful assistant",
        Temperature = 0.7,
        TopK = 5,
        MinConfidence = 0.7
    };
    private bool _isActive = true;
    private bool _shouldRecordInvocations;
    private int _invocationCount;

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

    public AgentBuilder WithType(AgentType type)
    {
        _type = type;
        return this;
    }

    public AgentBuilder AsQA()
    {
        _type = AgentType.QA;
        _name = "QA Agent";
        return this;
    }

    public AgentBuilder AsExplain()
    {
        _type = AgentType.Explain;
        _name = "Explain Agent";
        return this;
    }

    public AgentBuilder AsSetup()
    {
        _type = AgentType.Setup;
        _name = "Setup Agent";
        return this;
    }

    public AgentBuilder WithStrategy(AgentStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public AgentBuilder WithStrategy(
        string systemPrompt,
        double temperature = 0.7,
        int topK = 5,
        double minConfidence = 0.7)
    {
        _strategy = new AgentStrategy
        {
            SystemPrompt = systemPrompt,
            Temperature = temperature,
            TopK = topK,
            MinConfidence = minConfidence
        };
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

    public AgentBuilder WithInvocations(int count)
    {
        _shouldRecordInvocations = true;
        _invocationCount = count;
        return this;
    }

    /// <summary>
    /// Builds the Agent instance.
    /// </summary>
    public Agent Build()
    {
        var agent = new Agent(_id, _name, _type, _strategy, _isActive);

        if (_shouldRecordInvocations)
        {
            for (int i = 0; i < _invocationCount; i++)
            {
                if (!agent.IsActive)
                {
                    agent.Activate();
                }
                agent.RecordInvocation($"Test query {i + 1}", tokensUsed: 100);
            }

            if (!_isActive)
            {
                agent.Deactivate();
            }
        }

        return agent;
    }

    /// <summary>
    /// Implicit conversion to Agent for convenience.
    /// </summary>
    public static implicit operator Agent(AgentBuilder builder) => builder.Build();
}
