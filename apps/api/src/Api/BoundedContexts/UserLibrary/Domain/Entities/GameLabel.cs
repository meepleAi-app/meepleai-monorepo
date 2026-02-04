using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Represents a label that can be applied to games in a user's library.
/// Labels can be predefined (system-wide) or custom (user-created).
/// </summary>
internal sealed class GameLabel : Entity<Guid>
{
    /// <summary>
    /// Maximum length for label name.
    /// </summary>
    public const int MaxNameLength = 50;

    /// <summary>
    /// Maximum length for color hex code.
    /// </summary>
    public const int MaxColorLength = 7;

    /// <summary>
    /// The display name of the label.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Hex color code for the label (e.g., "#22c55e").
    /// </summary>
    public string Color { get; private set; }

    /// <summary>
    /// Whether this is a predefined system label.
    /// Predefined labels cannot be deleted or modified by users.
    /// </summary>
    public bool IsPredefined { get; private set; }

    /// <summary>
    /// The user who created this label (null for predefined labels).
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// When the label was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameLabel() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new custom label for a user.
    /// </summary>
    private GameLabel(Guid id, string name, string color, Guid userId) : base(id)
    {
        ValidateName(name);
        ValidateColor(color);

        Name = name.Trim();
        Color = color.Trim();
        IsPredefined = false;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new predefined (system) label.
    /// </summary>
    private GameLabel(Guid id, string name, string color) : base(id)
    {
        ValidateName(name);
        ValidateColor(color);

        Name = name.Trim();
        Color = color.Trim();
        IsPredefined = true;
        UserId = null;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a custom user label.
    /// </summary>
    public static GameLabel CreateCustom(Guid userId, string name, string color)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        return new GameLabel(Guid.NewGuid(), name, color, userId);
    }

    /// <summary>
    /// Factory method to create a predefined system label.
    /// </summary>
    public static GameLabel CreatePredefined(string name, string color)
    {
        return new GameLabel(Guid.NewGuid(), name, color);
    }

    /// <summary>
    /// Factory method to create a predefined system label with specific ID.
    /// Used for seeding consistent IDs.
    /// </summary>
    public static GameLabel CreatePredefinedWithId(Guid id, string name, string color)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty", nameof(id));

        return new GameLabel(id, name, color);
    }

    /// <summary>
    /// Updates the label name (only for custom labels).
    /// </summary>
    public void UpdateName(string name)
    {
        if (IsPredefined)
            throw new InvalidOperationException("Cannot modify predefined labels");

        ValidateName(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Updates the label color (only for custom labels).
    /// </summary>
    public void UpdateColor(string color)
    {
        if (IsPredefined)
            throw new InvalidOperationException("Cannot modify predefined labels");

        ValidateColor(color);
        Color = color.Trim();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Label name cannot be empty", nameof(name));

        if (name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Label name cannot exceed {MaxNameLength} characters", nameof(name));
    }

    private static void ValidateColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("Label color cannot be empty", nameof(color));

        var trimmed = color.Trim();
        if (trimmed.Length > MaxColorLength)
            throw new ArgumentException($"Label color cannot exceed {MaxColorLength} characters", nameof(color));

        // Validate hex color format
        if (!trimmed.StartsWith('#') || trimmed.Length != 7)
            throw new ArgumentException("Label color must be a valid hex color (e.g., #22c55e)", nameof(color));
    }
}
