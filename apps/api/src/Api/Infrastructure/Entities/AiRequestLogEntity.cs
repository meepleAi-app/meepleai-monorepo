namespace Api.Infrastructure.Entities;

public class AiRequestLogEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string? ApiKeyId { get; set; }
    public string? GameId { get; set; }
    public string Endpoint { get; set; } = default!; // "qa", "explain", "setup"
    public string? Query { get; set; }
    public string? ResponseSnippet { get; set; }
    public int LatencyMs { get; set; }
    public int TokenCount { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public double? Confidence { get; set; }
    public string Status { get; set; } = default!; // "Success", "Error"
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Model { get; set; }
    public string? FinishReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
