using Api.Infrastructure.Entities.Authentication;

namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// GameLabel entity - persistence model for game labels.
/// Labels can be predefined (system-wide) or custom (user-created).
/// </summary>
public class GameLabelEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The display name of the label.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hex color code for the label (e.g., "#22c55e").
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a predefined system label.
    /// </summary>
    public bool IsPredefined { get; set; }

    /// <summary>
    /// The user who created this label (null for predefined labels).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// When the label was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserEntity? User { get; set; }

    /// <summary>
    /// Games that have this label assigned.
    /// </summary>
    public ICollection<UserGameLabelEntity> GameLabels { get; set; } = new List<UserGameLabelEntity>();
}
