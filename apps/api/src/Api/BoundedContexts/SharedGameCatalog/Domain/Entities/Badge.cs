using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a badge definition in the gamification system.
/// Badges are awarded to contributors for various achievements.
/// </summary>
public sealed class Badge : Entity<Guid>
{
    private Guid _id;
    private readonly string _code;
    private string _name;
    private string _description;
    private string? _iconUrl;
    private readonly BadgeTier _tier;
    private readonly BadgeCategory _category;
    private bool _isActive;
    private int _displayOrder;
    private readonly BadgeRequirement _requirement;
    private readonly DateTime _createdAt;
    private DateTime? _modifiedAt;

    /// <summary>
    /// Gets the unique identifier of this badge.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the unique code for this badge (e.g., "FIRST_CONTRIBUTION").
    /// Used as a stable identifier for programmatic references.
    /// </summary>
    public string Code => _code;

    /// <summary>
    /// Gets the display name of this badge (e.g., "First Steps").
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the description of how this badge is earned.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Gets the URL to the badge icon image.
    /// </summary>
    public string? IconUrl => _iconUrl;

    /// <summary>
    /// Gets the tier level of this badge.
    /// </summary>
    public BadgeTier Tier => _tier;

    /// <summary>
    /// Gets the category of this badge.
    /// </summary>
    public BadgeCategory Category => _category;

    /// <summary>
    /// Gets whether this badge is currently active and can be earned.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the display order for sorting badges.
    /// </summary>
    public int DisplayOrder => _displayOrder;

    /// <summary>
    /// Gets the requirement for earning this badge.
    /// </summary>
    public BadgeRequirement Requirement => _requirement;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the last modification timestamp.
    /// </summary>
    public DateTime? ModifiedAt => _modifiedAt;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private Badge() : base()
    {
        _code = string.Empty;
        _name = string.Empty;
        _description = string.Empty;
        _requirement = BadgeRequirement.ForFirstContribution();
        _createdAt = DateTime.UtcNow;
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal Badge(
        Guid id,
        string code,
        string name,
        string description,
        string? iconUrl,
        BadgeTier tier,
        BadgeCategory category,
        bool isActive,
        int displayOrder,
        BadgeRequirement requirement,
        DateTime createdAt,
        DateTime? modifiedAt) : base(id)
    {
        _id = id;
        _code = code;
        _name = name;
        _description = description;
        _iconUrl = iconUrl;
        _tier = tier;
        _category = category;
        _isActive = isActive;
        _displayOrder = displayOrder;
        _requirement = requirement;
        _createdAt = createdAt;
        _modifiedAt = modifiedAt;
    }

    /// <summary>
    /// Creates a new badge definition.
    /// </summary>
    /// <param name="code">Unique code for the badge.</param>
    /// <param name="name">Display name for the badge.</param>
    /// <param name="description">Description of how to earn the badge.</param>
    /// <param name="tier">Tier level of the badge.</param>
    /// <param name="category">Category of the badge.</param>
    /// <param name="requirement">Requirement for earning the badge.</param>
    /// <param name="iconUrl">Optional URL to badge icon.</param>
    /// <param name="displayOrder">Optional display order (defaults to 0).</param>
    /// <returns>A new Badge instance.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid.</exception>
    public static Badge Create(
        string code,
        string name,
        string description,
        BadgeTier tier,
        BadgeCategory category,
        BadgeRequirement requirement,
        string? iconUrl = null,
        int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required", nameof(code));

        if (code.Length > 50)
            throw new ArgumentException("Code cannot exceed 50 characters", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (description.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        ArgumentNullException.ThrowIfNull(requirement);

        return new Badge(
            Guid.NewGuid(),
            code.ToUpperInvariant().Trim(),
            name.Trim(),
            description.Trim(),
            iconUrl?.Trim(),
            tier,
            category,
            isActive: true,
            displayOrder,
            requirement,
            DateTime.UtcNow,
            modifiedAt: null);
    }

    /// <summary>
    /// Updates the badge details.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    /// <param name="iconUrl">New icon URL.</param>
    /// <param name="displayOrder">New display order.</param>
    public void UpdateDetails(
        string name,
        string description,
        string? iconUrl,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        if (description.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        _name = name.Trim();
        _description = description.Trim();
        _iconUrl = iconUrl?.Trim();
        _displayOrder = displayOrder;
        _modifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the badge, allowing it to be earned.
    /// </summary>
    public void Activate()
    {
        if (_isActive) return;

        _isActive = true;
        _modifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the badge, preventing it from being earned.
    /// Existing earned badges are not affected.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive) return;

        _isActive = false;
        _modifiedAt = DateTime.UtcNow;
    }
}
