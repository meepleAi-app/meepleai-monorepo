using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure;

/// <summary>
/// Helper class for reading secrets from Docker Secrets files.
/// SEC-708: Docker Secrets implementation.
///
/// Supports the _FILE pattern convention where configuration can specify a file path
/// containing the secret value instead of the value itself.
///
/// Example usage:
///   var apiKey = SecretsHelper.GetSecretOrValue(config, "OPENROUTER_API_KEY", logger);
///
/// This will check:
///   1. OPENROUTER_API_KEY_FILE environment variable (path to secret file)
///   2. OPENROUTER_API_KEY environment variable (direct value)
///   3. Throws InvalidOperationException if neither is found
/// </summary>
internal static class SecretsHelper
{
    /// <summary>
    /// Gets a secret value from either a _FILE path or direct configuration value.
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <param name="key">Configuration key (e.g., "OPENROUTER_API_KEY")</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <param name="required">If true, throws when secret is not found. If false, returns null.</param>
    /// <returns>Secret value read from file or direct configuration</returns>
    /// <exception cref="InvalidOperationException">Thrown when required secret is not found</exception>
    /// <exception cref="FileNotFoundException">Thrown when _FILE path does not exist</exception>
    /// <exception cref="IOException">Thrown when file cannot be read</exception>
    public static string? GetSecretOrValue(
        IConfiguration config,
        string key,
        ILogger? logger = null,
        bool required = true)
    {
        var fileKey = $"{key}_FILE";
        var filePath = config[fileKey];

        // Priority 1: Read from secret file if _FILE env var exists
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            if (!File.Exists(filePath))
            {
                var error = $"Secret file not found: {filePath} (from {fileKey})";
                logger?.LogError("Secret file not found: {FilePath} (from {FileKey})", filePath, fileKey);
                throw new FileNotFoundException(error, filePath);
            }

            try
            {
                var secretValue = File.ReadAllText(filePath).Trim();

                if (string.IsNullOrWhiteSpace(secretValue))
                {
                    var error = $"Secret file is empty: {filePath} (from {fileKey})";
                    logger?.LogError("Secret file is empty: {FilePath} (from {FileKey})", filePath, fileKey);
                    throw new InvalidOperationException(error);
                }

                logger?.LogInformation(
                    "✅ Loaded secret '{Key}' from file: {FilePath} ({Length} bytes)",
                    key,
                    filePath,
                    secretValue.Length
                );

                return secretValue;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                var error = $"Failed to read secret file: {filePath} (from {fileKey})";
                logger?.LogError(ex, "Failed to read secret file: {FilePath} (from {FileKey})", filePath, fileKey);
                throw new IOException(error, ex);
            }
        }

        // Priority 2: Read from direct configuration value
        var directValue = config[key];

        if (!string.IsNullOrWhiteSpace(directValue))
        {
            logger?.LogInformation(
                "⚠️  Loaded secret '{Key}' from direct configuration (not recommended for production)",
                key
            );
            return directValue;
        }

        // Not found - throw or return null based on 'required' flag
        if (required)
        {
            var error = $"{key} not configured. Set either {key} or {fileKey} environment variable.";
            logger?.LogError("{ConfigKey} not configured. Set either {ConfigKey} or {FileKey} environment variable.", key, key, fileKey);
            throw new InvalidOperationException(error);
        }

        logger?.LogDebug("Secret '{Key}' not found (optional)", key);
        return null;
    }

    /// <summary>
    /// Builds a PostgreSQL connection string using password from secret file or config.
    /// </summary>
    /// <param name="config">Configuration instance</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Complete PostgreSQL connection string</returns>
    public static string BuildPostgresConnectionString(
        IConfiguration config,
        ILogger? logger = null)
    {
        var host = config["POSTGRES_HOST"] ?? "postgres";
        var port = config["POSTGRES_PORT"] ?? "5432";
        var database = config["POSTGRES_DB"] ?? config["ConnectionStrings:DefaultDatabase"] ?? "meepleai";
        var username = config["POSTGRES_USER"] ?? "meeple";

        // Get password from secret file or direct config
        var password = GetSecretOrValue(config, "POSTGRES_PASSWORD", logger, required: true);

        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

        logger?.LogInformation(
            "Built PostgreSQL connection string: Host={Host}, Database={Database}, User={User}",
            host,
            database,
            username
        );

        return connectionString;
    }
}
