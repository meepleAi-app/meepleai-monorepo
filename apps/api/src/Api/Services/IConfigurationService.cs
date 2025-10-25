using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for managing system-wide dynamic configuration.
/// Enables runtime configuration changes without redeployment.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Get all configurations with optional filtering.
    /// </summary>
    /// <param name="category">Optional category filter</param>
    /// <param name="environment">Optional environment filter</param>
    /// <param name="activeOnly">If true, return only active configurations</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <returns>Paginated list of configurations</returns>
    Task<PagedResult<SystemConfigurationDto>> GetConfigurationsAsync(
        string? category = null,
        string? environment = null,
        bool activeOnly = true,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Get a configuration by its unique ID.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <returns>Configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> GetConfigurationByIdAsync(string id);

    /// <summary>
    /// Get a configuration by its key.
    /// </summary>
    /// <param name="key">Configuration key (e.g., "RateLimit:Admin:MaxTokens")</param>
    /// <param name="environment">Optional environment filter (defaults to current environment)</param>
    /// <returns>Configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(string key, string? environment = null);

    /// <summary>
    /// Get a typed configuration value by key.
    /// </summary>
    /// <typeparam name="T">Expected value type</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <param name="environment">Optional environment filter</param>
    /// <returns>Typed configuration value or default</returns>
    Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null);

    /// <summary>
    /// Create a new configuration entry.
    /// </summary>
    /// <param name="request">Configuration creation request</param>
    /// <param name="userId">ID of the user creating the configuration</param>
    /// <returns>Created configuration DTO</returns>
    Task<SystemConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, string userId);

    /// <summary>
    /// Update an existing configuration.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="request">Update request with modified fields</param>
    /// <param name="userId">ID of the user updating the configuration</param>
    /// <returns>Updated configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> UpdateConfigurationAsync(string id, UpdateConfigurationRequest request, string userId);

    /// <summary>
    /// Delete a configuration by ID.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteConfigurationAsync(string id);

    /// <summary>
    /// Toggle a configuration's active status.
    /// </summary>
    /// <param name="id">Configuration ID</param>
    /// <param name="isActive">New active status</param>
    /// <param name="userId">ID of the user toggling the configuration</param>
    /// <returns>Updated configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> ToggleConfigurationAsync(string id, bool isActive, string userId);

    /// <summary>
    /// Bulk update multiple configurations atomically.
    /// </summary>
    /// <param name="request">Bulk update request</param>
    /// <param name="userId">ID of the user performing the bulk update</param>
    /// <returns>List of updated configurations</returns>
    Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync(
        BulkConfigurationUpdateRequest request,
        string userId);

    /// <summary>
    /// Validate a configuration value before applying it.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Value to validate</param>
    /// <param name="valueType">Type of the value</param>
    /// <returns>Validation result with any errors</returns>
    Task<ConfigurationValidationResult> ValidateConfigurationAsync(string key, string value, string valueType);

    /// <summary>
    /// Export configurations for a specific environment.
    /// </summary>
    /// <param name="environment">Environment to export (e.g., "Production")</param>
    /// <param name="activeOnly">If true, export only active configurations</param>
    /// <returns>Export DTO with configurations and metadata</returns>
    Task<ConfigurationExportDto> ExportConfigurationsAsync(string environment, bool activeOnly = true);

    /// <summary>
    /// Import configurations from an export file.
    /// </summary>
    /// <param name="request">Import request with configurations</param>
    /// <param name="userId">ID of the user performing the import</param>
    /// <returns>Number of configurations imported</returns>
    Task<int> ImportConfigurationsAsync(ConfigurationImportRequest request, string userId);

    /// <summary>
    /// Get configuration change history.
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <param name="limit">Maximum number of history entries to return</param>
    /// <returns>List of configuration history entries</returns>
    Task<IReadOnlyList<ConfigurationHistoryDto>> GetConfigurationHistoryAsync(string configurationId, int limit = 20);

    /// <summary>
    /// Rollback a configuration to a previous version.
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <param name="toVersion">Version number to rollback to</param>
    /// <param name="userId">ID of the user performing the rollback</param>
    /// <returns>Rolled back configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> RollbackConfigurationAsync(string configurationId, int toVersion, string userId);

    /// <summary>
    /// Get all unique categories across all configurations.
    /// </summary>
    /// <returns>List of category names</returns>
    Task<IReadOnlyList<string>> GetCategoriesAsync();

    /// <summary>
    /// Invalidate the configuration cache.
    /// Forces reload from database on next access.
    /// </summary>
    Task InvalidateCacheAsync();

    /// <summary>
    /// Invalidate cache for a specific configuration key.
    /// </summary>
    /// <param name="key">Configuration key to invalidate</param>
    Task InvalidateCacheAsync(string key);
}
