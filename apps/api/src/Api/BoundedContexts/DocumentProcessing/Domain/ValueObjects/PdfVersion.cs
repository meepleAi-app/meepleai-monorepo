using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// PDF version identifier (e.g., 1.4, 1.7, 2.0).
/// Represents the PDF specification version used to create the document.
/// </summary>
public sealed class PdfVersion : ValueObject
{
    /// <summary>
    /// Major version number (e.g., 1 for PDF 1.x, 2 for PDF 2.x)
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Minor version number (e.g., 4 for PDF 1.4, 7 for PDF 1.7)
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Creates a PDF version with the specified major and minor components.
    /// </summary>
    /// <param name="major">Major version (must be >= 1)</param>
    /// <param name="minor">Minor version (must be >= 0)</param>
    /// <exception cref="DomainException">Thrown if version numbers are invalid</exception>
    public PdfVersion(int major, int minor)
    {
        if (major < 1)
            throw new DomainException("PDF major version must be at least 1");

        if (minor < 0)
            throw new DomainException("PDF minor version must be non-negative");

        Major = major;
        Minor = minor;
    }

    /// <summary>
    /// Parses a PDF version string (e.g., "1.4", "1.7", "2.0").
    /// </summary>
    /// <param name="versionString">Version string to parse</param>
    /// <returns>PdfVersion instance</returns>
    /// <exception cref="DomainException">Thrown if version string is invalid</exception>
    public static PdfVersion Parse(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            throw new DomainException("PDF version string cannot be empty");

        var trimmed = versionString.Trim();

        // Try parsing as float (e.g., "1.4" → 1.4)
        if (float.TryParse(trimmed, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
        {
            var major = (int)floatValue;
            var minor = (int)Math.Round((floatValue - major) * 10);
            return new PdfVersion(major, minor);
        }

        // Try parsing as "major.minor" format
        var parts = trimmed.Split('.');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var majorPart) &&
            int.TryParse(parts[1], out var minorPart))
        {
            return new PdfVersion(majorPart, minorPart);
        }

        throw new DomainException($"Invalid PDF version format: '{versionString}'. Expected format: 'major.minor' (e.g., '1.4', '1.7')");
    }

    /// <summary>
    /// Attempts to parse a PDF version string without throwing exceptions.
    /// </summary>
    /// <param name="versionString">Version string to parse</param>
    /// <param name="version">Parsed version if successful</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParse(string? versionString, out PdfVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        try
        {
            version = Parse(versionString);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if this version is at least the specified version.
    /// </summary>
    /// <param name="other">Version to compare against</param>
    /// <returns>True if this version >= other version</returns>
    public bool IsAtLeast(PdfVersion other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        if (Major > other.Major)
            return true;

        if (Major == other.Major && Minor >= other.Minor)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if this version is compatible with the specified version.
    /// Compatibility rules: Same major version, this minor >= other minor.
    /// </summary>
    /// <param name="other">Version to check compatibility with</param>
    /// <returns>True if versions are compatible</returns>
    public bool IsCompatibleWith(PdfVersion other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        // Same major version and this minor >= other minor
        return Major == other.Major && Minor >= other.Minor;
    }

    /// <summary>
    /// Returns the version as a string (e.g., "1.4", "2.0").
    /// </summary>
    public override string ToString() => $"{Major}.{Minor}";

    /// <summary>
    /// Returns the version as a float (e.g., 1.4, 1.7, 2.0).
    /// </summary>
    public float ToFloat() => Major + (Minor / 10f);

    /// <summary>
    /// Equality comparison based on major and minor version.
    /// </summary>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Major;
        yield return Minor;
    }

    // Common PDF versions as static constants
    public static readonly PdfVersion Version10 = new(1, 0);
    public static readonly PdfVersion Version11 = new(1, 1);
    public static readonly PdfVersion Version12 = new(1, 2);
    public static readonly PdfVersion Version13 = new(1, 3);
    public static readonly PdfVersion Version14 = new(1, 4);
    public static readonly PdfVersion Version15 = new(1, 5);
    public static readonly PdfVersion Version16 = new(1, 6);
    public static readonly PdfVersion Version17 = new(1, 7);
    public static readonly PdfVersion Version20 = new(2, 0);
}
