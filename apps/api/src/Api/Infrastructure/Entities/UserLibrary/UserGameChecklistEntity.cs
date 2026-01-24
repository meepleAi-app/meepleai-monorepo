namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// GameChecklist entity - persistence model for game setup checklist items.
/// </summary>
public class UserGameChecklistEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to UserLibraryEntry (aggregate root).
    /// </summary>
    public Guid UserLibraryEntryId { get; set; }

    /// <summary>
    /// Description of the setup step.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Display order of this step in the checklist.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether this step is currently checked/completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Optional additional details or tips for this step.
    /// </summary>
    public string? AdditionalInfo { get; set; }

    /// <summary>
    /// When this checklist item was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this checklist item was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public UserLibraryEntryEntity? UserLibraryEntry { get; set; }
}
