namespace Api.Infrastructure.Entities;

internal class N8NConfigEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string ApiKeyEncrypted { get; set; } = default!;
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastTestedAt { get; set; }
    public string? LastTestResult { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid CreatedByUserId { get; set; }

    public UserEntity CreatedBy { get; set; } = default!;
}
