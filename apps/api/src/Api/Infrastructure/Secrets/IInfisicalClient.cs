using System.Threading.Tasks;

namespace Api.Infrastructure.Secrets;

/// <summary>
/// Infisical secrets management client interface for POC (Issue #936).
/// Provides access to secrets with versioning and rotation support.
/// </summary>
public interface IInfisicalClient
{
    /// <summary>
    /// Retrieves the current value of a secret by name.
    /// </summary>
    /// <param name="secretName">Name of the secret to retrieve</param>
    /// <param name="environment">Environment slug (e.g., "dev", "prod")</param>
    /// <param name="secretPath">Path to secret (default: "/")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current secret value</returns>
    Task<string> GetSecretAsync(
        string secretName,
        string environment = "dev",
        string secretPath = "/",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all versions of a secret for rollback capability testing.
    /// </summary>
    /// <param name="secretName">Name of the secret</param>
    /// <param name="environment">Environment slug</param>
    /// <param name="secretPath">Path to secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of secret versions (current + previous)</returns>
    Task<SecretVersion[]> GetSecretVersionsAsync(
        string secretName,
        string environment = "dev",
        string secretPath = "/",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connection to Infisical and validates authentication.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connected and authenticated successfully</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a specific version of a secret for rollback testing.
/// </summary>
public record SecretVersion(
    int Version,
    string Value,
    DateTime CreatedAt,
    string? CreatedBy);
