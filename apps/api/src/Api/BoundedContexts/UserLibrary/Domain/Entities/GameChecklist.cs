using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Represents a setup checklist item for a game in user's library.
/// Helps users prepare games for play with step-by-step guidance.
/// </summary>
internal sealed class GameChecklist : Entity<Guid>
{
    /// <summary>
    /// Foreign key to the UserLibraryEntry (aggregate root).
    /// </summary>
    public Guid UserLibraryEntryId { get; private set; }

    /// <summary>
    /// Description of the setup step (e.g., "Shuffle deck", "Deal cards").
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Display order of this step in the checklist (lower = earlier).
    /// </summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Whether this step is currently checked/completed.
    /// Resets to false after each game session.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Optional additional details or tips for this step.
    /// </summary>
    public string? AdditionalInfo { get; private set; }

    /// <summary>
    /// When this checklist item was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When this checklist item was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameChecklist() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new checklist item.
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="userLibraryEntryId">The library entry this checklist belongs to</param>
    /// <param name="description">Step description</param>
    /// <param name="displayOrder">Display order</param>
    /// <param name="additionalInfo">Optional details</param>
    /// <exception cref="ArgumentException">Thrown when invalid values are provided</exception>
    private GameChecklist(
        Guid id,
        Guid userLibraryEntryId,
        string description,
        int displayOrder,
        string? additionalInfo) : base(id)
    {
        if (userLibraryEntryId == Guid.Empty)
            throw new ArgumentException("UserLibraryEntryId cannot be empty", nameof(userLibraryEntryId));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (displayOrder < 0)
            throw new ArgumentException("DisplayOrder cannot be negative", nameof(displayOrder));

        UserLibraryEntryId = userLibraryEntryId;
        Description = description.Trim();
        DisplayOrder = displayOrder;
        AdditionalInfo = additionalInfo?.Trim();
        IsCompleted = false;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new checklist item.
    /// </summary>
    /// <param name="userLibraryEntryId">The library entry this checklist belongs to</param>
    /// <param name="description">Step description</param>
    /// <param name="displayOrder">Display order (0-based)</param>
    /// <param name="additionalInfo">Optional additional details</param>
    /// <returns>New GameChecklist instance</returns>
    public static GameChecklist Create(
        Guid userLibraryEntryId,
        string description,
        int displayOrder,
        string? additionalInfo = null)
    {
        return new GameChecklist(
            id: Guid.NewGuid(),
            userLibraryEntryId: userLibraryEntryId,
            description: description,
            displayOrder: displayOrder,
            additionalInfo: additionalInfo);
    }

    /// <summary>
    /// Marks this step as completed.
    /// </summary>
    public void MarkAsCompleted()
    {
        if (!IsCompleted)
        {
            IsCompleted = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Marks this step as incomplete.
    /// </summary>
    public void MarkAsIncomplete()
    {
        if (IsCompleted)
        {
            IsCompleted = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Toggles the completion state.
    /// </summary>
    public void Toggle()
    {
        IsCompleted = !IsCompleted;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the description of this step.
    /// </summary>
    /// <param name="description">New description</param>
    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        Description = description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the display order of this step.
    /// </summary>
    /// <param name="displayOrder">New display order value</param>
    public void UpdateOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new ArgumentException("DisplayOrder cannot be negative", nameof(displayOrder));

        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the additional info for this step.
    /// </summary>
    /// <param name="additionalInfo">New additional info (or null to clear)</param>
    public void UpdateAdditionalInfo(string? additionalInfo)
    {
        AdditionalInfo = additionalInfo?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets the completion state (typically called before a new game session).
    /// </summary>
    public void ResetCompletion()
    {
        IsCompleted = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Moves this item up in the order (decreases display order by 1).
    /// </summary>
    public void MoveUp()
    {
        if (DisplayOrder > 0)
        {
            DisplayOrder--;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Moves this item down in the order (increases display order by 1).
    /// </summary>
    public void MoveDown()
    {
        DisplayOrder++;
        UpdatedAt = DateTime.UtcNow;
    }
}
