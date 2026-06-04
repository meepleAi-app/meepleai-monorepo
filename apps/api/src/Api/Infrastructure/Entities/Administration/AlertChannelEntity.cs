namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// Persistence entity for the AlertChannel aggregate (Issue #1840 SP5 F4-C7).
///
/// <para>One row per channel type (email | slack) — see the
/// <c>alert_channels.type</c> primary key. Schema kept narrow on purpose:
/// the channel-specific config lives in <see cref="ConfigJson"/> so adding
/// a new channel type doesn't require an additive migration.</para>
/// </summary>
public class AlertChannelEntity
{
    /// <summary>Channel discriminator ("email" | "slack"). Primary key.</summary>
    public required string Type { get; set; }

    /// <summary>JSON blob carrying the transport config; shape varies by <see cref="Type"/>.</summary>
    public required string ConfigJson { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? LastTestedAt { get; set; }

    /// <summary>"ok" | "error" — null when never tested.</summary>
    public string? LastTestStatus { get; set; }

    public string? LastTestMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>EF Core optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
