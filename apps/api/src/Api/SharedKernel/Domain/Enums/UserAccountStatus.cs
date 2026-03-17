namespace Api.SharedKernel.Domain.Enums;

/// <summary>
/// User account status shared across bounded contexts.
/// Used by Authentication (User entity) and Administration (permission checks).
/// </summary>
public enum UserAccountStatus
{
    Active = 0,
    Suspended = 1,
    Banned = 2,
    Pending = 3
}
