using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Configuration;

/// <summary>
/// Loads and validates secrets from directory-based secret files
/// </summary>
/// <remarks>
/// ISSUE-2510: Secrets management system with 3-level validation
///
/// Supports KEY=VALUE format with:
/// - Empty lines ignored
/// - Comments starting with # ignored
/// - Whitespace trimmed
///
/// Example secret file (database.secret):
/// <code>
/// # PostgreSQL credentials
/// POSTGRES_USER=meepleai
/// POSTGRES_PASSWORD=secure_password_here
/// POSTGRES_DB=meepleai_db
/// </code>
/// </remarks>
internal sealed class SecretLoader
{
    private readonly ILogger _logger;
    private readonly string _secretsDirectory;

    /// <summary>
    /// Initialize secret loader
    /// </summary>
    /// <param name="configuration">Application configuration (for directory path override)</param>
    /// <param name="logger">Logger instance</param>
    public SecretLoader(IConfiguration configuration, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        // Allow directory override via configuration (default: infra/secrets)
        _secretsDirectory = configuration["SecretsDirectory"] ?? "infra/secrets";

        // Convert to absolute path if relative
        if (!Path.IsPathRooted(_secretsDirectory))
        {
            _secretsDirectory = Path.Combine(Directory.GetCurrentDirectory(), _secretsDirectory);
        }
    }

    /// <summary>
    /// Load and validate all secrets according to SecretDefinitions
    /// </summary>
    /// <returns>Validation result with loaded secrets and missing items</returns>
    public SecretValidationResult LoadAndValidate()
    {
        _logger.LogInformation("Loading secrets from directory: {SecretsDirectory}", _secretsDirectory);

        var result = new SecretValidationResult();

        // Check if secrets directory exists
        if (!Directory.Exists(_secretsDirectory))
        {
            _logger.LogWarning("Secrets directory not found: {SecretsDirectory}", _secretsDirectory);
            CollectAllMissingSecrets(result);
            return result;
        }

        // Process each secret definition
        foreach (var (secretName, secretSpec) in SecretDefinitions.All)
        {
            ProcessSecretFile(secretName, secretSpec, result);
        }

        // Log summary
        LogValidationSummary(result);

        return result;
    }

    /// <summary>
    /// Process a single secret file
    /// </summary>
    private void ProcessSecretFile(string secretName, SecretSpec secretSpec, SecretValidationResult result)
    {
        var secretFile = Path.Combine(_secretsDirectory, $"{secretName}.secret");

        if (!File.Exists(secretFile))
        {
            HandleMissingSecretFile(secretName, secretSpec, result);
            return;
        }

        try
        {
            var secrets = ParseSecretFile(secretFile);
            ValidateRequiredKeys(secretName, secretSpec, secrets, result);
            result.LoadedSecrets.Add(secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse secret file: {SecretFile}", secretFile);
            result.ParseErrors[secretName] = ex.Message;

            // Treat parse errors as missing secrets
            HandleMissingSecretFile(secretName, secretSpec, result);
        }
    }

    /// <summary>
    /// Parse secret file into key-value dictionary
    /// </summary>
    /// <returns>Dictionary of KEY=VALUE pairs</returns>
    private Dictionary<string, string> ParseSecretFile(string filePath)
    {
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmedLine = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            // Parse KEY=VALUE
            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                _logger.LogWarning("Invalid line format in {FilePath}: {Line}", filePath, trimmedLine);
                continue;
            }

            var key = trimmedLine[..separatorIndex].Trim();
            var value = trimmedLine[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Empty key in {FilePath}: {Line}", filePath, trimmedLine);
                continue;
            }

            secrets[key] = value;
        }

        return secrets;
    }

    /// <summary>
    /// Validate that all required keys are present
    /// </summary>
    private void ValidateRequiredKeys(
        string secretName,
        SecretSpec secretSpec,
        Dictionary<string, string> loadedSecrets,
        SecretValidationResult result)
    {
        foreach (var requiredKey in secretSpec.RequiredKeys)
        {
            if (!loadedSecrets.TryGetValue(requiredKey, out var value) || string.IsNullOrWhiteSpace(value))
            {
                var fullKeyName = $"{secretName}.secret:{requiredKey}";
                AddMissingSecret(fullKeyName, secretSpec.Level, result);
            }
        }
    }

    /// <summary>
    /// Handle missing secret file based on level
    /// </summary>
    private void HandleMissingSecretFile(string secretName, SecretSpec secretSpec, SecretValidationResult result)
    {
        foreach (var requiredKey in secretSpec.RequiredKeys)
        {
            var fullKeyName = $"{secretName}.secret:{requiredKey}";
            AddMissingSecret(fullKeyName, secretSpec.Level, result);
        }
    }

    /// <summary>
    /// Add missing secret to appropriate list based on level
    /// </summary>
    private static void AddMissingSecret(string keyName, SecretLevel level, SecretValidationResult result)
    {
        switch (level)
        {
            case SecretLevel.Critical:
                result.MissingCritical.Add(keyName);
                break;
            case SecretLevel.Important:
                result.MissingImportant.Add(keyName);
                break;
            case SecretLevel.Optional:
                result.MissingOptional.Add(keyName);
                break;
        }
    }

    /// <summary>
    /// Collect all secrets as missing (when directory doesn't exist)
    /// </summary>
    private static void CollectAllMissingSecrets(SecretValidationResult result)
    {
        foreach (var (secretName, secretSpec) in SecretDefinitions.All)
        {
            foreach (var requiredKey in secretSpec.RequiredKeys)
            {
                var fullKeyName = $"{secretName}.secret:{requiredKey}";
                AddMissingSecret(fullKeyName, secretSpec.Level, result);
            }
        }
    }

    /// <summary>
    /// Log validation summary
    /// </summary>
    private void LogValidationSummary(SecretValidationResult result)
    {
        _logger.LogInformation(
            "Secret validation complete: {Loaded} loaded, {Critical} critical missing, {Important} important missing, {Optional} optional missing",
            result.LoadedSecrets.Count,
            result.MissingCritical.Count,
            result.MissingImportant.Count,
            result.MissingOptional.Count
        );

        // Log parse errors
        foreach (var (secretName, error) in result.ParseErrors)
        {
            _logger.LogWarning("Parse error in {SecretName}: {Error}", secretName, error);
        }

        // Log missing critical secrets (ERROR level)
        if (result.MissingCritical.Count > 0)
        {
            _logger.LogError(
                "CRITICAL secrets missing (startup will fail): {MissingSecrets}",
                string.Join(", ", result.MissingCritical)
            );
        }

        // Log missing important secrets (WARNING level)
        if (result.MissingImportant.Count > 0)
        {
            _logger.LogWarning(
                "IMPORTANT secrets missing (reduced functionality): {MissingSecrets}",
                string.Join(", ", result.MissingImportant)
            );
        }

        // Log missing optional secrets (INFO level)
        if (result.MissingOptional.Count > 0)
        {
            _logger.LogInformation(
                "OPTIONAL secrets missing (some features disabled): {MissingSecrets}",
                string.Join(", ", result.MissingOptional)
            );
        }
    }
}
