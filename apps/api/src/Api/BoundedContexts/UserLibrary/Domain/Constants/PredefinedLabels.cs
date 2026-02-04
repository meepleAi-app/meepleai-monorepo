namespace Api.BoundedContexts.UserLibrary.Domain.Constants;

/// <summary>
/// Predefined system labels for game categorization.
/// These labels are available to all users and cannot be modified or deleted.
/// </summary>
internal static class PredefinedLabels
{
    /// <summary>
    /// Family-friendly games suitable for all ages.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Family =
        (Guid.Parse("00000000-0000-0000-0001-000000000001"), "Family", "#22c55e");

    /// <summary>
    /// Strategy games requiring planning and tactical thinking.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Strategy =
        (Guid.Parse("00000000-0000-0000-0001-000000000002"), "Strategy", "#3b82f6");

    /// <summary>
    /// Party games designed for social gatherings.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Party =
        (Guid.Parse("00000000-0000-0000-0001-000000000003"), "Party", "#f59e0b");

    /// <summary>
    /// Cooperative games where players work together.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Cooperative =
        (Guid.Parse("00000000-0000-0000-0001-000000000004"), "Cooperative", "#8b5cf6");

    /// <summary>
    /// Games designed for solo play.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Solo =
        (Guid.Parse("00000000-0000-0000-0001-000000000005"), "Solo", "#ec4899");

    /// <summary>
    /// Quick filler games for short play sessions.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Filler =
        (Guid.Parse("00000000-0000-0000-0001-000000000006"), "Filler", "#6b7280");

    /// <summary>
    /// Classic games with timeless appeal.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Classic =
        (Guid.Parse("00000000-0000-0000-0001-000000000007"), "Classic", "#78716c");

    /// <summary>
    /// Games designed for children.
    /// </summary>
    public static readonly (Guid Id, string Name, string Color) Kids =
        (Guid.Parse("00000000-0000-0000-0001-000000000008"), "Kids", "#14b8a6");

    /// <summary>
    /// Gets all predefined labels.
    /// </summary>
    public static IReadOnlyList<(Guid Id, string Name, string Color)> All => new[]
    {
        Family,
        Strategy,
        Party,
        Cooperative,
        Solo,
        Filler,
        Classic,
        Kids
    };

    /// <summary>
    /// Checks if the given ID is a predefined label ID.
    /// </summary>
    public static bool IsPredefinedId(Guid id)
    {
        return All.Any(l => l.Id == id);
    }
}
