namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// EF persistence model for the <c>staging_allowlist</c> table (#845).
/// Domain twin: <see cref="Api.BoundedContexts.Administration.Domain.Entities.StagingAllowlistEntry"/>.
/// </summary>
public class StagingAllowlistEntity
{
    public Guid Id { get; set; }

    /// <summary>Normalized (trimmed + lowercase) email.</summary>
    public required string Email { get; set; }

    /// <summary>User who added the entry; <c>null</c> for system bootstrap seeds.</summary>
    public Guid? AddedByUserId { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }
}
