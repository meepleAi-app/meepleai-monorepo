namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Immutable snapshot of a PlayRecord's editable details (SessionDate, Notes, Location)
/// for version history + restore (#2437-3).
/// </summary>
/// <remarks>
/// Captured BEFORE each UpdateDetails call, so each version represents a prior state
/// that can be restored. The first update captures the initial creation state.
/// Cap: 5 most-recent versions per record (pruned post-update by the handler).
/// Pattern cloned from ToolkitVersion (GameToolkit).
/// </remarks>
internal sealed class PlayRecordVersion : SharedKernel.Domain.Entities.Entity<Guid>
{
    public Guid PlayRecordId { get; private set; }
    public int VersionNumber { get; private set; }
    public DateTime SessionDate { get; private set; }
    public string? Notes { get; private set; }
    public string? Location { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    /// <summary>EF Core parameterless constructor.</summary>
#pragma warning disable CS8618
    private PlayRecordVersion() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Creates a new version snapshot of a PlayRecord's editable details.
    /// </summary>
    /// <param name="id">New unique id for this version row.</param>
    /// <param name="playRecordId">Parent PlayRecord id (must not be empty).</param>
    /// <param name="versionNumber">Monotonically increasing version counter for this record.</param>
    /// <param name="sessionDate">SessionDate value at the time of the snapshot.</param>
    /// <param name="notes">Notes value at the time of the snapshot (nullable).</param>
    /// <param name="location">Location value at the time of the snapshot (nullable).</param>
    /// <param name="createdAt">UTC timestamp when the snapshot was created.</param>
    /// <param name="createdByUserId">User who triggered the update that caused this snapshot.</param>
    internal PlayRecordVersion(
        Guid id,
        Guid playRecordId,
        int versionNumber,
        DateTime sessionDate,
        string? notes,
        string? location,
        DateTime createdAt,
        Guid createdByUserId) : base(id)
    {
        if (playRecordId == Guid.Empty)
            throw new ArgumentException("PlayRecordId cannot be empty.", nameof(playRecordId));

        PlayRecordId = playRecordId;
        VersionNumber = versionNumber;
        SessionDate = sessionDate;
        Notes = notes;
        Location = location;
        CreatedAt = createdAt;
        CreatedByUserId = createdByUserId;
    }
}
