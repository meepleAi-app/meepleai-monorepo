using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Issue #1903 M7.1 — tracks the SHA-256 hash of the BoardGameGeek terms of
/// service page. Singleton row (<see cref="SingletonId"/>) updated monthly by
/// <c>BggTosWatcherJob</c>. When the hash changes a warning is logged so that
/// operators can perform the manual legal review required by spec §8.5.6
/// (pre-rollout legal checklist).
/// </summary>
public class BggTosHashEntity
{
    /// <summary>
    /// Fixed primary key for the singleton row. Encoded as
    /// <c>00000000-1903-4007-8000-000000000001</c> so the version nibble
    /// (4=random) and the issue number remain visible in DB dumps.
    /// </summary>
    public static readonly Guid SingletonId = new("00000000-1903-4007-8000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>Hexadecimal SHA-256 of the ToS HTML (lower case, 64 chars).</summary>
    [MaxLength(64)]
    public string CurrentHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the most recent check (success or hash-changed).</summary>
    public DateTime LastCheckedAt { get; set; }

    /// <summary>UTC timestamp of the last detected hash change, null on first run.</summary>
    public DateTime? LastChangedAt { get; set; }

    /// <summary>Cumulative count of detected hash changes since first observation.</summary>
    public int ChangeCount { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
