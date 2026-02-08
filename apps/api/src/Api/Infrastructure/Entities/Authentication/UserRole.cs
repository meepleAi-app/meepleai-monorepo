namespace Api.Infrastructure.Entities;

/// <summary>
/// User roles for authorization and access control.
/// Explicit values preserve backward compatibility with existing database records.
/// Issue #3690: Added SuperAdmin with value 3 to avoid shifting existing role values.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Operations, monitoring, user management (no global feature flags)
    /// </summary>
    Admin = 0,

    /// <summary>
    /// Content management (games, PDFs), limited analytics (no operations)
    /// </summary>
    Editor = 1,

    /// <summary>
    /// Standard user with basic access
    /// </summary>
    User = 2,

    /// <summary>
    /// Full system access, can manage other admins and global feature flags
    /// </summary>
    SuperAdmin = 3
}
