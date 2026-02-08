using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Entity representing a player in a play record.
/// Can be a registered user (UserId not null) or an external guest (UserId null).
/// </summary>
internal sealed class RecordPlayer : Entity<Guid>
{
    public Guid PlayRecordId { get; private set; }
    public Guid? UserId { get; private set; }
    public string DisplayName { get; private set; }

    private readonly List<RecordScore> _scores = new();
    public IReadOnlyList<RecordScore> Scores => _scores.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private RecordPlayer() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new player in a play record.
    /// </summary>
    /// <param name="id">Player identifier</param>
    /// <param name="playRecordId">Parent play record ID</param>
    /// <param name="userId">Optional user ID (null for guests)</param>
    /// <param name="displayName">Display name</param>
    internal RecordPlayer(
        Guid id,
        Guid playRecordId,
        Guid? userId,
        string displayName) : base(id)
    {
        if (playRecordId == Guid.Empty)
            throw new ArgumentException("PlayRecordId cannot be empty", nameof(playRecordId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ValidationException("Display name cannot be empty");

        var trimmed = displayName.Trim();
        if (trimmed.Length > 255)
            throw new ValidationException("Display name cannot exceed 255 characters");

        PlayRecordId = playRecordId;
        UserId = userId;
        DisplayName = trimmed;
    }

    /// <summary>
    /// Records a score for this player in a specific dimension.
    /// Replaces existing score for the same dimension.
    /// </summary>
    internal void RecordScore(RecordScore score)
    {
        ArgumentNullException.ThrowIfNull(score);

        // Remove existing score for this dimension
        var existing = _scores.FirstOrDefault(s =>
            string.Equals(s.Dimension, score.Dimension, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            _scores.Remove(existing);

        _scores.Add(score);
    }

    /// <summary>
    /// Gets the score for a specific dimension, if recorded.
    /// </summary>
    public RecordScore? GetScore(string dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension))
            return null;

        return _scores.FirstOrDefault(s =>
            string.Equals(s.Dimension, dimension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if this player is a registered user.
    /// </summary>
    public bool IsRegisteredUser => UserId.HasValue;

    /// <summary>
    /// Checks if this player is an external guest.
    /// </summary>
    public bool IsGuest => !UserId.HasValue;
}
