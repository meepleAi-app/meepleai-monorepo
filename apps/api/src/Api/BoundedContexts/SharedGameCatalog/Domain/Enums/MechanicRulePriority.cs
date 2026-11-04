namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>Ordered precedence level of a claim. Higher wins. House rules live in AgentMemory and beat all.</summary>
public enum MechanicRulePriority
{
    Base = 0,
    Expansion = 1,
    Card = 2,
    Scenario = 3
}
