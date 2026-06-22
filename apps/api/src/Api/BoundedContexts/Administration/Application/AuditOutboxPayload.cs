namespace Api.BoundedContexts.Administration.Application;

/// <summary>
/// Serialized contract of an audit_outbox payload. Produced by
/// <see cref="Behaviors.AuditLoggingBehavior{TRequest,TResponse}"/>, consumed by
/// AuditOutboxProcessor (T4) to materialize an AuditLogEntity row.
///
/// PascalCase keys are intentional — JSON round-trips cleanly with
/// <c>JsonSerializer.Deserialize&lt;AuditOutboxPayload&gt;</c> using default options.
/// </summary>
public sealed record AuditOutboxPayload
{
    public required string Action { get; init; }
    public required string Resource { get; init; }
    public string? UserId { get; init; }
    public string? ResourceId { get; init; }

    /// <summary>"Success" or "Error".</summary>
    public required string Result { get; init; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? RequestType { get; init; }

    /// <summary>Existing metadata JSON (adminEmail, commandType, errorMessage, etc.).</summary>
    public string? Details { get; init; }

    /// <summary>Per-entity before/after snapshots captured by the interceptor.</summary>
    public IReadOnlyList<AuditSnapshotPayload> Snapshots { get; init; } = [];

    /// <summary>Populated by S2 (impersonation); null until then.</summary>
    public Guid? ImpersonatedUserId { get; init; }

    /// <summary>Populated by S3 (step-up token); null until then.</summary>
    public Guid? StepUpTokenId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// True when at least one snapshot was flagged <c>_oversize: true</c> by
    /// <c>PayloadTruncator</c>. The T4 processor must mark rows with <c>Oversize = true</c>
    /// as Failed with <c>last_error = "payload_oversize"</c> rather than persisting the truncated data.
    /// </summary>
    public bool Oversize { get; init; }
}

/// <summary>Per-entity before/after snapshot inside an audit payload.</summary>
public sealed record AuditSnapshotPayload
{
    public required string EntityType { get; init; }
    public required string PrimaryKey { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }

    /// <summary>"Insert", "Update", or "Delete".</summary>
    public required string Operation { get; init; }
}
