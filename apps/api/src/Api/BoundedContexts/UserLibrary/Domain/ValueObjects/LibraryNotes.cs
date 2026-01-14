using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Value object representing personal notes for a game in user's library.
/// Notes are optional and limited to 500 characters.
/// </summary>
internal sealed class LibraryNotes : ValueObject
{
    private const int MaxLength = 500;

    /// <summary>
    /// The notes content.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new LibraryNotes value object.
    /// </summary>
    /// <param name="notes">The notes content (max 500 characters)</param>
    /// <exception cref="ArgumentException">Thrown when notes exceed max length</exception>
    public LibraryNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("Notes cannot be empty or whitespace", nameof(notes));

        var trimmed = notes.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Notes cannot exceed {MaxLength} characters", nameof(notes));

        Value = trimmed;
    }

    /// <summary>
    /// Creates LibraryNotes from a nullable string, returning null if empty/whitespace.
    /// </summary>
    public static LibraryNotes? FromNullable(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;
        return new LibraryNotes(notes);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(LibraryNotes notes) => notes.Value;
}
