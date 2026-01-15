using System.Globalization;
using System.Text.RegularExpressions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Value object representing a template version.
/// Format: MAJOR.MINOR (e.g., "1.0", "1.1", "2.0")
/// </summary>
public sealed partial class TemplateVersion : ValueObject, IComparable<TemplateVersion>, IEquatable<TemplateVersion>
{
    private static readonly Regex VersionPattern = VersionRegex();

    /// <summary>
    /// Gets the version string.
    /// </summary>
    public string Value { get; private set; }

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; private set; }

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public int Minor { get; private set; }

    /// <summary>
    /// Private constructor for EF Core and internal use.
    /// </summary>
    private TemplateVersion()
    {
        Value = "1.0";
        Major = 1;
        Minor = 0;
    }

    /// <summary>
    /// Private constructor for creating a template version.
    /// </summary>
    private TemplateVersion(string value, int major, int minor)
    {
        Value = value;
        Major = major;
        Minor = minor;
    }

    /// <summary>
    /// Creates a new TemplateVersion with validation.
    /// </summary>
    /// <param name="version">The version string in format MAJOR.MINOR</param>
    /// <returns>A new TemplateVersion instance</returns>
    /// <exception cref="ArgumentException">Thrown when version format is invalid</exception>
    public static TemplateVersion Create(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required", nameof(version));

        version = version.Trim();

        if (!VersionPattern.IsMatch(version))
            throw new ArgumentException("Version must be in format MAJOR.MINOR (e.g., 1.0, 2.1)", nameof(version));

        var parts = version.Split('.');
        var major = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minor = int.Parse(parts[1], CultureInfo.InvariantCulture);

        if (major < 0 || minor < 0)
            throw new ArgumentException("Version numbers cannot be negative", nameof(version));

        return new TemplateVersion(version, major, minor);
    }

    /// <summary>
    /// Creates the default version 1.0.
    /// </summary>
    public static TemplateVersion Default => new("1.0", 1, 0);

    /// <summary>
    /// Creates a new version by incrementing the minor version.
    /// </summary>
    public TemplateVersion IncrementMinor() =>
        new($"{Major}.{Minor + 1}", Major, Minor + 1);

    /// <summary>
    /// Creates a new version by incrementing the major version and resetting minor.
    /// </summary>
    public TemplateVersion IncrementMajor() =>
        new($"{Major + 1}.0", Major + 1, 0);

    /// <summary>
    /// Compares two versions.
    /// </summary>
    /// <returns>
    /// -1 if this version is less than other,
    /// 0 if they are equal,
    /// 1 if this version is greater than other
    /// </returns>
    public int CompareTo(TemplateVersion? other)
    {
        if (other is null)
            return 1;
        if (Major != other.Major)
            return Major.CompareTo(other.Major);
        return Minor.CompareTo(other.Minor);
    }

    /// <summary>
    /// Returns true if this version is greater than the other.
    /// </summary>
    public bool IsNewerThan(TemplateVersion? other) => CompareTo(other) > 0;

    /// <inheritdoc/>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => base.Equals(obj);

    /// <inheritdoc/>
    public bool Equals(TemplateVersion? other) => base.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => base.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(TemplateVersion version) => version.Value;

    // Comparison operators
    public static bool operator ==(TemplateVersion? left, TemplateVersion? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(TemplateVersion? left, TemplateVersion? right) =>
        !(left == right);

    public static bool operator <(TemplateVersion left, TemplateVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(TemplateVersion left, TemplateVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(TemplateVersion left, TemplateVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(TemplateVersion left, TemplateVersion right) =>
        left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^\d+\.\d+$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionRegex();
}
