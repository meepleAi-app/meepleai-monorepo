namespace Api.Infrastructure.Entities;

public class AgentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!; // AgentType.Value
    public string StrategyName { get; set; } = default!;
    public string StrategyParametersJson { get; set; } = "{}"; // JSON serialized Dictionary<string, object>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastInvokedAt { get; set; }
    public int InvocationCount { get; set; }
}