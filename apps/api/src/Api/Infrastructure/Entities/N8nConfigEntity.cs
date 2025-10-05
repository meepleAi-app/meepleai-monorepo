namespace Api.Infrastructure.Entities;

public class N8nConfigEntity
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string ApiKeyEncrypted { get; set; } = default!;
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastTestedAt { get; set; }
    public string? LastTestResult { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = default!;

    public UserEntity CreatedBy { get; set; } = default!;
}
