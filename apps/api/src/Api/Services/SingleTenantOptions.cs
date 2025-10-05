namespace Api.Services;

/// <summary>
/// Configuration for single-tenant deployments.
/// </summary>
public class SingleTenantOptions
{
    /// <summary>
    /// Identifier persisted with all tenant-scoped entities.
    /// Defaults to "meepleai" when not provided.
    /// </summary>
    public string TenantId { get; set; } = "meepleai";

    /// <summary>
    /// Human readable tenant name used for metadata entries.
    /// Falls back to <see cref="TenantId"/> when omitted.
    /// </summary>
    public string? TenantName { get; set; }

    /// <summary>
    /// Returns the effective tenant identifier ensuring a non-empty value.
    /// </summary>
    public string GetTenantId() => string.IsNullOrWhiteSpace(TenantId)
        ? "meepleai"
        : TenantId.Trim();

    /// <summary>
    /// Returns the effective tenant display name ensuring a non-empty value.
    /// </summary>
    public string GetTenantName()
    {
        var tenantId = GetTenantId();
        return string.IsNullOrWhiteSpace(TenantName)
            ? tenantId
            : TenantName.Trim();
    }
}
