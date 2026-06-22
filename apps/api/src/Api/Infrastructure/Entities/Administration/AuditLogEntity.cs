namespace Api.Infrastructure.Entities;

public class AuditLogEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string Resource { get; set; } = default!;
    public string? ResourceId { get; set; }
    public string Result { get; set; } = default!; // Success, Denied, Error
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // SP5 Admin Security S1: snapshot + traceability columns
    /// <summary>Snapshot JSON of the entity state BEFORE the mutation. Null for create operations.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>Snapshot JSON of the entity state AFTER the mutation. Null for delete operations.</summary>
    public string? AfterJson { get; set; }

    /// <summary>When set, the actor user was impersonating this user at the time of the mutation. Populated by S2.</summary>
    public Guid? ImpersonatedUserId { get; set; }

    /// <summary>When set, the mutation was gated by a step-up token. Populated by S3.</summary>
    public Guid? StepUpTokenId { get; set; }
}
