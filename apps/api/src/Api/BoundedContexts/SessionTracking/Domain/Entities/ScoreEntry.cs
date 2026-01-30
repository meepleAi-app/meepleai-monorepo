using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Score entry entity representing a score for a participant in a session.
/// Supports both round-based and category-based scoring.
/// </summary>
public class ScoreEntry
{
    /// <summary>
    /// Score entry unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Session reference.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Participant reference.
    /// </summary>
    public Guid ParticipantId { get; private set; }

    /// <summary>
    /// Round number (nullable for category-only scoring).
    /// </summary>
    public int? RoundNumber { get; private set; }

    /// <summary>
    /// Score category (nullable for round-only scoring).
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; private set; }

    /// <summary>
    /// Score value with decimal precision (10,2).
    /// </summary>
    public decimal ScoreValue { get; private set; }

    /// <summary>
    /// When the score was recorded.
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created this score entry.
    /// </summary>
    public Guid CreatedBy { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private ScoreEntry()
    {
    }

    /// <summary>
    /// Factory method to create a score entry.
    /// </summary>
    /// <param name="sessionId">Session reference.</param>
    /// <param name="participantId">Participant reference.</param>
    /// <param name="scoreValue">Score value.</param>
    /// <param name="createdBy">User creating the score.</param>
    /// <param name="roundNumber">Optional round number.</param>
    /// <param name="category">Optional category.</param>
    /// <returns>New score entry instance.</returns>
    public static ScoreEntry Create(
        Guid sessionId,
        Guid participantId,
        decimal scoreValue,
        Guid createdBy,
        int? roundNumber = null,
        string? category = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("Creator ID cannot be empty.", nameof(createdBy));

        if (roundNumber.HasValue && roundNumber.Value <= 0)
            throw new ArgumentException("Round number must be positive.", nameof(roundNumber));

        if (!string.IsNullOrWhiteSpace(category) && category.Length > 50)
            throw new ArgumentException("Category cannot exceed 50 characters.", nameof(category));

        // At least one of roundNumber or category must be provided
        if (!roundNumber.HasValue && string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Either round number or category must be provided.", nameof(roundNumber));

        return new ScoreEntry
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = participantId,
            ScoreValue = scoreValue,
            RoundNumber = roundNumber,
            Category = !string.IsNullOrWhiteSpace(category) ? category.Trim() : null,
            CreatedBy = createdBy,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the score value.
    /// </summary>
    /// <param name="newValue">New score value.</param>
    public void UpdateScore(decimal newValue)
    {
        ScoreValue = newValue;
        Timestamp = DateTime.UtcNow;
    }
}