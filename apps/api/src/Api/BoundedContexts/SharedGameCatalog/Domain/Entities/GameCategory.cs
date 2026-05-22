using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a game category (e.g., Strategy, Family, Party).
/// Categories are used for game classification and filtering.
/// </summary>
public sealed class GameCategory : Entity<Guid>
{
    private Guid _id;
    private readonly string _name = string.Empty;
    private readonly string _slug = string.Empty;
    private readonly string? _emoji;
    private readonly string? _color;

    /// <summary>
    /// Gets the unique identifier of this category.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the display name of the category.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the URL-friendly slug of the category.
    /// </summary>
    public string Slug => _slug;

    /// <summary>
    /// Gets the optional emoji glyph used as visual marker (#1440).
    /// </summary>
    public string? Emoji => _emoji;

    /// <summary>
    /// Gets the optional 6-digit hex color (#RRGGBB) for chip rendering (#1440).
    /// </summary>
    public string? Color => _color;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private GameCategory() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal GameCategory(Guid id, string name, string slug, string? emoji = null, string? color = null) : base(id)
    {
        _id = id;
        _name = name;
        _slug = slug;
        _emoji = emoji;
        _color = color;
    }

    /// <summary>
    /// Creates a new GameCategory with validation.
    /// </summary>
    public static GameCategory Create(string name, string slug, string? emoji = null, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required", nameof(name));

        if (name.Length > 100)
            throw new ArgumentException("Category name cannot exceed 100 characters", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Category slug is required", nameof(slug));

        if (slug.Length > 100)
            throw new ArgumentException("Category slug cannot exceed 100 characters", nameof(slug));

        return new GameCategory(Guid.NewGuid(), name, slug, emoji, color);
    }
}
