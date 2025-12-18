using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Document type value object representing the category of a PDF document within a game collection.
/// Issue #2051: Support for base rulebooks, expansions, errata, and house rules
/// </summary>
internal sealed class DocumentType : ValueObject
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "base",
        "expansion",
        "errata",
        "homerule"
    };

    /// <summary>
    /// Priority for citation ordering (higher number = higher priority)
    /// Priority logic: homerule (3) > errata/expansion by date (2/1) > base (0)
    /// </summary>
    private static readonly Dictionary<string, int> TypePriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        { "base", 0 },
        { "expansion", 1 },
        { "errata", 2 },
        { "homerule", 3 }
    };

    public string Value { get; }
    public int Priority => TypePriorities[Value];

    public DocumentType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ValidationException(nameof(DocumentType), "Document type cannot be empty");

        var normalized = type.Trim().ToLowerInvariant();

        if (!ValidTypes.Contains(normalized))
            throw new ValidationException(nameof(DocumentType),
                $"Document type must be one of: {string.Join(", ", ValidTypes.OrderBy(x => x, StringComparer.Ordinal))}. Got: {type}");

        Value = normalized;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    // Convenience factory methods
    public static DocumentType Base => new("base");
    public static DocumentType Expansion => new("expansion");
    public static DocumentType Errata => new("errata");
    public static DocumentType Homerule => new("homerule");

    public static bool IsValid(string type) =>
        !string.IsNullOrWhiteSpace(type) && ValidTypes.Contains(type.Trim().ToLowerInvariant());

    public static IReadOnlySet<string> GetValidTypes() => ValidTypes;

    // Priority comparison helpers
    public bool HasHigherPriorityThan(DocumentType other) => Priority > other.Priority;
    public bool HasLowerPriorityThan(DocumentType other) => Priority < other.Priority;
}
