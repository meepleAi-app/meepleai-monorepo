namespace Api.Infrastructure.Secrets;

/// <summary>
/// Configuration options for Infisical secrets client (Issue #936 POC).
/// </summary>
internal class InfisicalOptions
{
    public const string SectionName = "Infisical";

    /// <summary>
    /// Infisical server URL.
    /// Example: http://localhost:8081 (self-hosted), https://app.infisical.com (cloud)
    /// </summary>
    public string HostUrl { get; set; } = "http://localhost:8081";

    /// <summary>
    /// Universal Auth Client ID from Machine Identity.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Universal Auth Client Secret from Machine Identity.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Project ID (workspace ID) where secrets are stored.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
}
