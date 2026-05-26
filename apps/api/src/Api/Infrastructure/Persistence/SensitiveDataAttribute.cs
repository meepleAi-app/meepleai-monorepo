namespace Api.Infrastructure.Persistence;

/// <summary>
/// Marks an entity property whose value must NOT appear verbatim in audit snapshots.
/// The <see cref="AuditingSaveChangesInterceptor"/> replaces such values with
/// <see cref="AuditingSaveChangesInterceptor.RedactedPlaceholder"/> before serialization.
/// Apply to credential/secret fields (password hashes, encrypted TOTP secrets, API key material).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute { }
