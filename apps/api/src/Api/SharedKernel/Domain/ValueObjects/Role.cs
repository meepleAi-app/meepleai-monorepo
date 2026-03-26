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

    public bool IsAdmin() => string.Equals(Value, "admin", StringComparison.Ordinal);
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
        if (IsAdmin() && !requiredRole.IsSuperAdmin()) return true;

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
