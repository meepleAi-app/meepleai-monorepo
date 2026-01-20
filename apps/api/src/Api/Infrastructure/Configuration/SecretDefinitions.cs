namespace Api.Infrastructure.Configuration;

/// <summary>
/// Severity level for secret validation
/// </summary>
/// <remarks>
/// ISSUE-2510: 3-level validation system
/// </remarks>
internal enum SecretLevel
{
    /// <summary>
    /// Critical: Must exist, startup blocked if missing
    /// Examples: Database credentials, Redis password, JWT secret
    /// </summary>
    Critical,

    /// <summary>
    /// Important: Should exist, warning logged if missing but startup continues
    /// Examples: OpenRouter API key, BGG credentials, Unstructured API key
    /// </summary>
    Important,

    /// <summary>
    /// Optional: Nice to have, info logged if missing
    /// Examples: OAuth credentials, SMTP settings, monitoring passwords
    /// </summary>
    Optional
}

/// <summary>
/// Specification for a secret file
/// </summary>
internal sealed record SecretSpec(
    SecretLevel Level,
    params string[] RequiredKeys
);

/// <summary>
/// Static definitions of all secrets required by the application
/// </summary>
/// <remarks>
/// ISSUE-2510: Centralized secret specifications
/// Each secret file should contain KEY=VALUE pairs (one per line)
/// Files are located in infra/secrets/ directory
/// </remarks>
internal static class SecretDefinitions
{
    /// <summary>
    /// All secret specifications indexed by file name (without .secret extension)
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SecretSpec> All = new Dictionary<string, SecretSpec>(StringComparer.Ordinal)
    {
        // ===== CRITICAL SECRETS (Startup blocked if missing) =====

        ["database"] = new(
            SecretLevel.Critical,
            "POSTGRES_USER",
            "POSTGRES_PASSWORD",
            "POSTGRES_DB"
        ),

        ["redis"] = new(
            SecretLevel.Critical,
            "REDIS_PASSWORD"
        ),

        ["qdrant"] = new(
            SecretLevel.Critical,
            "QDRANT_API_KEY"
        ),

        ["jwt"] = new(
            SecretLevel.Critical,
            "JWT_SECRET_KEY",
            "JWT_ISSUER",
            "JWT_AUDIENCE"
        ),

        ["admin"] = new(
            SecretLevel.Critical,
            "ADMIN_EMAIL",
            "ADMIN_PASSWORD",
            "ADMIN_DISPLAY_NAME"
        ),

        ["embedding-service"] = new(
            SecretLevel.Critical,
            "EMBEDDING_SERVICE_API_KEY"
        ),

        // ===== IMPORTANT SECRETS (Warnings if missing) =====

        ["openrouter"] = new(
            SecretLevel.Important,
            "OPENROUTER_API_KEY",
            "OPENROUTER_DEFAULT_MODEL"
        ),

        ["unstructured-service"] = new(
            SecretLevel.Important,
            "UNSTRUCTURED_API_KEY"
        ),

        ["bgg"] = new(
            SecretLevel.Important,
            "BGG_USERNAME",
            "BGG_PASSWORD"
        ),

        // ===== OPTIONAL SECRETS (Info if missing) =====

        ["oauth"] = new(
            SecretLevel.Optional,
            "GOOGLE_OAUTH_CLIENT_ID",
            "GOOGLE_OAUTH_CLIENT_SECRET",
            "GITHUB_OAUTH_CLIENT_ID",
            "GITHUB_OAUTH_CLIENT_SECRET",
            "DISCORD_OAUTH_CLIENT_ID",
            "DISCORD_OAUTH_CLIENT_SECRET"
        ),

        ["email"] = new(
            SecretLevel.Optional,
            "SMTP_HOST",
            "SMTP_PORT",
            "SMTP_USER",
            "SMTP_PASSWORD",
            "SMTP_FROM_EMAIL",
            "GMAIL_APP_PASSWORD"
        ),

        ["storage"] = new(
            SecretLevel.Optional,
            "S3_ACCESS_KEY",
            "S3_SECRET_KEY",
            "S3_BUCKET_NAME",
            "S3_REGION"
        ),

        ["monitoring"] = new(
            SecretLevel.Optional,
            "GRAFANA_ADMIN_PASSWORD",
            "PROMETHEUS_PASSWORD",
            "SLACK_WEBHOOK_URL"
        ),

        ["traefik"] = new(
            SecretLevel.Optional,
            "TRAEFIK_DASHBOARD_USER",
            "TRAEFIK_DASHBOARD_PASSWORD"
        ),

        ["smoldocling-service"] = new(
            SecretLevel.Optional,
            "SMOLDOCLING_API_KEY"
        ),

        ["reranker-service"] = new(
            SecretLevel.Optional,
            "RERANKER_API_KEY"
        ),

        ["n8n"] = new(
            SecretLevel.Optional,
            "N8N_ENCRYPTION_KEY",
            "N8N_BASIC_AUTH_PASSWORD"
        )
    };

    /// <summary>
    /// Get secrets grouped by level
    /// </summary>
    public static IEnumerable<(string Name, SecretSpec Spec)> GetByLevel(SecretLevel level)
    {
        return All.Where(kvp => kvp.Value.Level == level)
                  .Select(kvp => (kvp.Key, kvp.Value));
    }
}
