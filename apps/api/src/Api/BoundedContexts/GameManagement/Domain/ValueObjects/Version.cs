using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing semantic version (major.minor.patch).
/// </summary>
public sealed class Version : ValueObject
{
    // FIX MA0009: Add timeout to prevent ReDoS attacks
    // FIX MA0023: Add ExplicitCapture to prevent capturing unneeded groups
    private static readonly Regex VersionRegex = new(@"^(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string Value { get; }

    public Version(int major, int minor, int patch)
    {
        if (major < 0)
            throw new ValidationException("Major version cannot be negative");
        if (minor < 0)
            throw new ValidationException("Minor version cannot be negative");
        if (patch < 0)
            throw new ValidationException("Patch version cannot be negative");

        Major = major;
        Minor = minor;
        Patch = patch;
        Value = $"{major}.{minor}.{patch}";
    }

    public Version(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ValidationException("Version cannot be empty");

        var match = VersionRegex.Match(version.Trim());
        if (!match.Success)
            throw new ValidationException($"Invalid version format: {version}. Expected: major.minor.patch (e.g., 1.0.0)");

        Major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        Minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        Patch = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        Value = version.Trim();
    }

    /// <summary>
    /// Increments major version and resets minor and patch to 0.
    /// </summary>
    public Version IncrementMajor() => new Version(Major + 1, 0, 0);

    /// <summary>
    /// Increments minor version and resets patch to 0.
    /// </summary>
    public Version IncrementMinor() => new Version(Major, Minor + 1, 0);

    /// <summary>
    /// Increments patch version.
    /// </summary>
    public Version IncrementPatch() => new Version(Major, Minor, Patch + 1);

    /// <summary>
    /// Compares two versions for ordering.
    /// </summary>
    public int CompareTo(Version other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }

    public bool IsNewerThan(Version other) => CompareTo(other) > 0;
    public bool IsOlderThan(Version other) => CompareTo(other) < 0;

    public static Version Initial => new Version(1, 0, 0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Major;
        yield return Minor;
        yield return Patch;
    }

    public override string ToString() => Value;

    public static implicit operator string(Version version) => version.Value;
}
