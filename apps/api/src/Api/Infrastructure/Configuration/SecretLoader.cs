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
    private readonly Dictionary<string, string> _loadedValues = new(StringComparer.Ordinal);

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

        // Priority order for secrets directory:
        // 1. Environment variable SECRETS_DIRECTORY (absolute path)
        // 2. Configuration setting SecretsDirectory
        // 3. Auto-detect monorepo root + infra/secrets
        var envSecretsDir = Environment.GetEnvironmentVariable("SECRETS_DIRECTORY");
        var configSecretsDir = configuration["SecretsDirectory"];

        if (!string.IsNullOrWhiteSpace(envSecretsDir))
        {
            _secretsDirectory = envSecretsDir;
            _logger.LogDebug("Using secrets directory from SECRETS_DIRECTORY env var: {Directory}", _secretsDirectory);
        }
        else if (!string.IsNullOrWhiteSpace(configSecretsDir))
        {
            _secretsDirectory = configSecretsDir;
            _logger.LogDebug("Using secrets directory from configuration: {Directory}", _secretsDirectory);
        }
        else
        {
            // Auto-detect monorepo root by finding infra/secrets or .git directory
            _secretsDirectory = FindMonorepoSecretsDirectory() ?? "infra/secrets";
            _logger.LogDebug("Using auto-detected secrets directory: {Directory}", _secretsDirectory);
        }

        // Convert to absolute path if relative
        if (!Path.IsPathRooted(_secretsDirectory))
        {
            _secretsDirectory = Path.Combine(Directory.GetCurrentDirectory(), _secretsDirectory);
        }
    }

    /// <summary>
    /// Find the secrets directory by searching up the directory tree for the monorepo root.
    /// Looks for infra/secrets directory or .git marker.
    /// </summary>
    /// <returns>Absolute path to secrets directory if found, null otherwise</returns>
    private static string? FindMonorepoSecretsDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var searchDir = new DirectoryInfo(currentDir);

        // Search up to 10 levels up (safety limit)
        for (int i = 0; i < 10 && searchDir != null; i++)
        {
            // Check if infra/secrets exists at this level
            var secretsPath = Path.Combine(searchDir.FullName, "infra", "secrets");
            if (Directory.Exists(secretsPath))
            {
                return secretsPath;
            }

            // Check if .git exists (monorepo root marker)
            var gitPath = Path.Combine(searchDir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                // Found repo root, return infra/secrets path (may not exist yet)
                return Path.Combine(searchDir.FullName, "infra", "secrets");
            }

            searchDir = searchDir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Load and validate all secrets according to SecretDefinitions.
    /// Also loads values into environment variables for use by the application.
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

        // Apply loaded values as environment variables (for Development/Staging)
        // This ensures secrets are available to the application without Docker env_file
        ApplyAsEnvironmentVariables();

        // Log summary
        LogValidationSummary(result);

        return result;
    }

    /// <summary>
    /// Apply all loaded secret values as environment variables.
    /// Only sets variables that are not already set (preserves existing env vars).
    /// This enables local development without Docker's env_file mechanism.
    /// </summary>
    private void ApplyAsEnvironmentVariables()
    {
        var applied = 0;
        var skipped = 0;

        foreach (var (key, value) in _loadedValues)
        {
            // Don't override existing environment variables
            var existingValue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(existingValue))
            {
                _logger.LogDebug("Skipping {Key}: already set in environment", key);
                skipped++;
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
            applied++;
        }

        _logger.LogInformation(
            "Applied {Applied} secret values as environment variables ({Skipped} skipped - already set)",
            applied, skipped);
    }

    /// <summary>
    /// Get all loaded secret values as a dictionary.
    /// Useful for adding to IConfiguration if needed.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetLoadedValues() => _loadedValues;

    /// <summary>
    /// Get only secret values that are NOT already present in the existing configuration snapshot.
    /// This prevents file-based secrets from overriding env_file / environment values
    /// that were already set by Docker Compose or the hosting environment.
    /// </summary>
    /// <remarks>
    /// Only checks the IConfiguration snapshot (captured at CreateBuilder time), NOT live
    /// Environment.GetEnvironmentVariable(). This is important because ApplyAsEnvironmentVariables()
    /// may have already set env vars for NEW values — checking live env vars would incorrectly
    /// skip those values, leaving them absent from IConfiguration.
    /// </remarks>
    /// <param name="existingConfiguration">Current IConfiguration snapshot to check against</param>
    /// <returns>Dictionary of secret values that are new (not already in the configuration snapshot)</returns>
    public IReadOnlyDictionary<string, string> GetNewValues(IConfiguration existingConfiguration)
    {
        var newValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var skipped = 0;

        foreach (var (key, value) in _loadedValues)
        {
            // Only check the IConfiguration snapshot (env vars captured at CreateBuilder time).
            // This reflects Docker env_file and environment: values — the authoritative source.
            var existingValue = existingConfiguration[key];

            if (!string.IsNullOrEmpty(existingValue))
            {
                _logger.LogDebug("Secret '{Key}' already in configuration snapshot, not overriding", key);
                skipped++;
                continue;
            }

            newValues[key] = value;
        }

        _logger.LogInformation(
            "Secret values for IConfiguration: {New} new, {Skipped} skipped (already configured)",
            newValues.Count, skipped);

        return newValues;
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

            // Store all parsed values for later application as environment variables
            foreach (var (key, value) in secrets)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _loadedValues[key] = value;
                }
            }
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
