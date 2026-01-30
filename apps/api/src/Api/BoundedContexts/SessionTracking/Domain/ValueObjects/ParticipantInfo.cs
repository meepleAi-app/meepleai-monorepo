namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing participant information.
/// </summary>
public record ParticipantInfo
{
    /// <summary>
    /// Display name for the participant.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Indicates if participant is the session owner.
    /// </summary>
    public bool IsOwner { get; init; }

    /// <summary>
    /// Order in which participant joined (1-based).
    /// </summary>
    public int JoinOrder { get; init; }

    /// <summary>
    /// Private constructor for validation.
    /// </summary>
    private ParticipantInfo() { }

    /// <summary>
    /// Factory method to create validated ParticipantInfo.
    /// </summary>
    /// <param name="displayName">Participant display name.</param>
    /// <param name="isOwner">Is session owner flag.</param>
    /// <param name="joinOrder">Join order (1-based).</param>
    /// <returns>Validated ParticipantInfo instance.</returns>
    public static ParticipantInfo Create(string displayName, bool isOwner, int joinOrder)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

        if (displayName.Length > 50)
            throw new ArgumentException("Display name cannot exceed 50 characters.", nameof(displayName));

        if (joinOrder <= 0)
            throw new ArgumentException("Join order must be positive.", nameof(joinOrder));

        return new ParticipantInfo
        {
            DisplayName = displayName.Trim(),
            IsOwner = isOwner,
            JoinOrder = joinOrder
        };
    }
}