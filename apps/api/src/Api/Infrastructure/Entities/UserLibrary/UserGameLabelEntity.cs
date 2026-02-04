namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// UserGameLabel entity - persistence model for the junction between UserLibraryEntry and GameLabel.
/// Represents the assignment of a label to a specific game in a user's library.
/// </summary>
public class UserGameLabelEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The ID of the library entry this label is assigned to.
    /// </summary>
    public Guid UserLibraryEntryId { get; set; }

    /// <summary>
    /// The ID of the label assigned to the game.
    /// </summary>
    public Guid LabelId { get; set; }

    /// <summary>
    /// When this label was assigned to the game.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserLibraryEntryEntity? UserLibraryEntry { get; set; }
    public GameLabelEntity? Label { get; set; }
}
