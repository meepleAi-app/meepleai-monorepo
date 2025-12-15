using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Represents a user role in the system.
/// </summary>
internal sealed class Role : ValueObject
{
    public static readonly Role User = new("user");
    public static readonly Role Editor = new("editor");
    public static readonly Role Admin = new("admin");

    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "user", "editor", "admin"
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
            throw new ValidationException(nameof(Role), $"Invalid role: {value}. Valid roles are: user, editor, admin");

        return new Role(normalized);
    }

    public bool IsAdmin() => string.Equals(Value, "admin", StringComparison.Ordinal);
    public bool IsEditor() => string.Equals(Value, "editor", StringComparison.Ordinal);
    public bool IsUser() => string.Equals(Value, "user", StringComparison.Ordinal);

    public bool HasPermission(Role requiredRole)
    {
        // Admin has all permissions
        if (IsAdmin()) return true;

        // Editor has editor + user permissions
        if (IsEditor() && (requiredRole.IsEditor() || requiredRole.IsUser())) return true;

        // User has user permissions only
        return string.Equals(Value, requiredRole.Value, StringComparison.Ordinal);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Role role) => role.Value;
}
