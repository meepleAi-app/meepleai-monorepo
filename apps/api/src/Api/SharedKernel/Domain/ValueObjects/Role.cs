using Api.SharedKernel.Domain.Exceptions;

namespace Api.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Represents a user role in the system.
/// </summary>
public sealed class Role : ValueObject
{
    public static readonly Role User = new("user");
    public static readonly Role Editor = new("editor");
    public static readonly Role Creator = new("creator"); // Epic #4068
    public static readonly Role Admin = new("admin");
    public static readonly Role SuperAdmin = new("superadmin");

    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "user", "editor", "creator", "admin", "superadmin"
    };

    public string Value { get; }

    private Role(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static Role Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(Role), "Role cannot be empty");

        var normalized = value.ToLowerInvariant();
        if (!ValidRoles.Contains(normalized))
            throw new ValidationException(nameof(Role), $"Invalid role: {value}. Valid roles are: user, creator, editor, admin, superadmin");

        return new Role(normalized);
    }

    /// <summary>
    /// Parses a role without throwing, for the callers that receive the role as a raw
    /// string coming from outside the domain (claims, DTOs, config) and must decide what
    /// an absent or unknown value means. Returns false — i.e. fails closed — for null,
    /// whitespace and any value outside <see cref="ValidRoles"/>.
    /// </summary>
    public static bool TryParse(string? value, out Role role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = User;
            return false;
        }

        var normalized = value.ToLowerInvariant();
        if (!ValidRoles.Contains(normalized))
        {
            role = User;
            return false;
        }

        role = new Role(normalized);
        return true;
    }

    /// <summary>
    /// True only for the role <c>admin</c> itself — NOT for <c>superadmin</c>, which outranks it.
    /// </summary>
    /// <remarks>
    /// ISSUE #3994 — this was called <c>IsAdmin()</c> until it was renamed, and the old name is why
    /// the same defect shipped three times (#3842, #3873, #4003): the backend also has a
    /// <c>ClaimsPrincipal.IsAdmin()</c> extension whose semantics are the OPPOSITE on this exact
    /// point — it answers <c>IsInRole("Admin") || IsInRole("SuperAdmin")</c>. Two identically named
    /// predicates disagreeing about superadmin meant a call site could not be read without first
    /// resolving the type of the receiver, so "exclude superadmin" kept being written by accident.
    /// <para>
    /// The name now states it. For "admin privileges or above" use
    /// <c>HasPermission(Role.Admin)</c> = {admin, superadmin}; this predicate is for the few places
    /// that genuinely need identity, such as a dispatch over the five roles.
    /// </para>
    /// <para>
    /// Renaming rather than removing is deliberate: <c>HasPermission</c> itself uses it (below), so
    /// removing it would put a raw literal in the canonical place, and
    /// <c>FeatureFlagService</c> dispatches over all five identity predicates — dropping one would
    /// leave four predicates plus a literal. The asymmetry with the other four names is the point:
    /// this is the only one with a homonym.
    /// </para>
    /// </remarks>
    public bool IsExactlyAdmin() => string.Equals(Value, "admin", StringComparison.Ordinal);
    public bool IsEditor() => string.Equals(Value, "editor", StringComparison.Ordinal);
    public bool IsCreator() => string.Equals(Value, "creator", StringComparison.Ordinal); // Epic #4068
    public bool IsUser() => string.Equals(Value, "user", StringComparison.Ordinal);
    public bool IsSuperAdmin() => string.Equals(Value, "superadmin", StringComparison.Ordinal);

    public bool HasPermission(Role requiredRole)
    {
        ArgumentNullException.ThrowIfNull(requiredRole);
        // SuperAdmin has all permissions
        if (IsSuperAdmin()) return true;

        // Admin has all permissions except SuperAdmin
        if (IsExactlyAdmin() && !requiredRole.IsSuperAdmin()) return true;

        // Creator has creator + user permissions (Epic #4068)
        if (IsCreator() && (requiredRole.IsCreator() || requiredRole.IsUser())) return true;

        // Editor has editor + creator + user permissions
        if (IsEditor() && (requiredRole.IsEditor() || requiredRole.IsCreator() || requiredRole.IsUser())) return true;

        // User has user permissions only
        return string.Equals(Value, requiredRole.Value, StringComparison.Ordinal);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return role.Value;
    }
}
