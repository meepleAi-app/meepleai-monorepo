using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for managing system-wide dynamic configuration.
/// Provides runtime configuration changes with caching and audit trail.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    private const string CacheKeyPrefix = "config:";
    private const string CacheCategoryTagPrefix = "config:category:";
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);

    public ConfigurationService(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<ConfigurationService> logger,
        IWebHostEnvironment environment,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _environment = environment;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PagedResult<SystemConfigurationDto>> GetConfigurationsAsync(
        string? category = null,
        string? environment = null,
        bool activeOnly = true,
        int page = 1,
        int pageSize = 50)
    {
        var query = _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .Include(c => c.UpdatedBy)
            .AsNoTracking();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(c => c.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(c => c.Environment == environment || c.Environment == "All");
        }

        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        // Sorting by updated date (most recent first)
        query = query.OrderByDescending(c => c.UpdatedAt);

        var total = await query.CountAsync();
        var configurations = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<SystemConfigurationDto>(
            Items: configurations.Select(MapToDto).ToList(),
            Total: total,
            Page: page,
            PageSize: pageSize
        );
    }

    public async Task<SystemConfigurationDto?> GetConfigurationByIdAsync(string id)
    {
        var config = await _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .Include(c => c.UpdatedBy)
            .AsNoTrackingWithIdentityResolution()
            .FirstOrDefaultAsync(c => c.Id == id);

        return config != null ? MapToDto(config) : null;
    }

    public async Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(string key, string? environment = null)
    {
        var currentEnvironment = environment ?? _environment.EnvironmentName;

        // Try to get from cache first
        var cacheKey = GetCacheKey(key, currentEnvironment);
        var cachedValue = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var config = await _dbContext.SystemConfigurations
                    .Include(c => c.CreatedBy)
                    .Include(c => c.UpdatedBy)
                    .AsNoTrackingWithIdentityResolution()
                    .Where(c => c.Key == key && c.IsActive)
                    .Where(c => c.Environment == currentEnvironment || c.Environment == "All")
                    .OrderBy(c => c.Environment == currentEnvironment ? 0 : 1) // Prioritize environment-specific
                    .FirstOrDefaultAsync(cancel);

                return config != null ? MapToDto(config) : null;
            },
            tags: [CacheCategoryTagPrefix + "general"],
            expiration: DefaultCacheDuration,
            ct: CancellationToken.None
        );

        return cachedValue;
    }

    public async Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null)
    {
        var config = await GetConfigurationByKeyAsync(key, environment);

        if (config == null)
        {
            _logger.LogDebug("Configuration {Key} not found, returning default value", key);
            return defaultValue;
        }

        try
        {
            return DeserializeValue<T>(config.Value, config.ValueType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize configuration {Key} of type {ValueType}", key, config.ValueType);
            return defaultValue;
        }
    }

    public async Task<SystemConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, string userId)
    {
        // Validate uniqueness of (key, environment)
        var exists = await _dbContext.SystemConfigurations
            .AnyAsync(c => c.Key == request.Key && c.Environment == request.Environment);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Configuration with key '{request.Key}' already exists for environment '{request.Environment}'");
        }

        // Validate the value
        var validationResult = await ValidateConfigurationAsync(request.Key, request.Value, request.ValueType);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Configuration validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        var entity = new SystemConfigurationEntity
        {
            Id = Guid.NewGuid().ToString(),
            Key = request.Key,
            Value = request.Value,
            ValueType = request.ValueType,
            Description = request.Description,
            Category = request.Category,
            IsActive = request.IsActive,
            RequiresRestart = request.RequiresRestart,
            Environment = request.Environment,
            Version = 1,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            CreatedByUserId = userId
        };

        _dbContext.SystemConfigurations.Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Configuration {Key} created by user {UserId} for environment {Environment}",
            request.Key, userId, request.Environment);

        // Invalidate cache for this key
        await InvalidateCacheAsync(request.Key);

        // Load navigation properties for DTO
        await _dbContext.Entry(entity).Reference(e => e.CreatedBy).LoadAsync();

        return MapToDto(entity);
    }

    public async Task<SystemConfigurationDto?> UpdateConfigurationAsync(
        string id,
        UpdateConfigurationRequest request,
        string userId)
    {
        var entity = await _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            return null;
        }

        // Store previous value before update
        var previousValue = entity.Value;

        // Apply updates
        if (request.Value != null)
        {
            // Validate the new value
            var valueType = request.ValueType ?? entity.ValueType;
            var validationResult = await ValidateConfigurationAsync(entity.Key, request.Value, valueType);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(
                    $"Configuration validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            entity.Value = request.Value;
            entity.PreviousValue = previousValue;
        }

        if (request.ValueType != null)
        {
            entity.ValueType = request.ValueType;
        }

        if (request.Description != null)
        {
            entity.Description = request.Description;
        }

        if (request.Category != null)
        {
            entity.Category = request.Category;
        }

        if (request.IsActive.HasValue && request.IsActive.Value != entity.IsActive)
        {
            entity.IsActive = request.IsActive.Value;
            entity.LastToggledAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        if (request.RequiresRestart.HasValue)
        {
            entity.RequiresRestart = request.RequiresRestart.Value;
        }

        if (request.Environment != null)
        {
            entity.Environment = request.Environment;
        }

        // Increment version and update metadata
        entity.Version++;
        entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        entity.UpdatedByUserId = userId;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Configuration {Key} (ID: {Id}) updated to version {Version} by user {UserId}",
            entity.Key, id, entity.Version, userId);

        // Invalidate cache for this key
        await InvalidateCacheAsync(entity.Key);

        // Load updated navigation properties
        await _dbContext.Entry(entity).Reference(e => e.UpdatedBy).LoadAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteConfigurationAsync(string id)
    {
        var entity = await _dbContext.SystemConfigurations.FindAsync(id);

        if (entity == null)
        {
            return false;
        }

        var key = entity.Key;

        _dbContext.SystemConfigurations.Remove(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogWarning("Configuration {Key} (ID: {Id}) deleted", key, id);

        // Invalidate cache for this key
        await InvalidateCacheAsync(key);

        return true;
    }

    public async Task<SystemConfigurationDto?> ToggleConfigurationAsync(string id, bool isActive, string userId)
    {
        var entity = await _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
        {
            return null;
        }

        if (entity.IsActive != isActive)
        {
            entity.IsActive = isActive;
            entity.LastToggledAt = _timeProvider.GetUtcNow().UtcDateTime;
            entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            entity.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Configuration {Key} (ID: {Id}) toggled to {Status} by user {UserId}",
                entity.Key, id, isActive ? "active" : "inactive", userId);

            // Invalidate cache for this key
            await InvalidateCacheAsync(entity.Key);

            // Load updated navigation properties
            await _dbContext.Entry(entity).Reference(e => e.UpdatedBy).LoadAsync();
        }

        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync(
        BulkConfigurationUpdateRequest request,
        string userId)
    {
        // Use a transaction for atomicity
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var updatedConfigs = new List<SystemConfigurationDto>();

            foreach (var update in request.Updates)
            {
                var entity = await _dbContext.SystemConfigurations.FindAsync(update.Id);

                if (entity == null)
                {
                    throw new InvalidOperationException($"Configuration with ID {update.Id} not found");
                }

                // Validate the new value
                var validationResult = await ValidateConfigurationAsync(entity.Key, update.Value, entity.ValueType);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Validation failed for {entity.Key}: {string.Join(", ", validationResult.Errors)}");
                }

                entity.PreviousValue = entity.Value;
                entity.Value = update.Value;
                entity.Version++;
                entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                entity.UpdatedByUserId = userId;

                updatedConfigs.Add(MapToDto(entity));
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Bulk update completed for {Count} configurations by user {UserId}",
                request.Updates.Count, userId);

            // Invalidate all affected caches
            foreach (var config in updatedConfigs)
            {
                await InvalidateCacheAsync(config.Key);
            }

            return updatedConfigs;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Bulk configuration update failed for user {UserId}. Attempting rollback. Updates: {UpdateCount}",
                userId,
                request.Updates.Count);

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback transaction after bulk update error for user {UserId}",
                    userId);
                // Don't throw rollback exception - original exception is more important
            }

            throw; // Re-throw original exception
        }
    }

    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(
        string key,
        string value,
        string valueType)
    {
        var errors = new List<string>();

        // Validate based on type
        try
        {
            switch (valueType.ToLower())
            {
                case "int":
                case "integer":
                    if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        errors.Add($"Value '{value}' is not a valid integer");
                    }
                    break;

                case "long":
                    if (!long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        errors.Add($"Value '{value}' is not a valid long integer");
                    }
                    break;

                case "double":
                case "float":
                    if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        errors.Add($"Value '{value}' is not a valid decimal number");
                    }
                    break;

                case "bool":
                case "boolean":
                    if (!bool.TryParse(value, out _))
                    {
                        errors.Add($"Value '{value}' is not a valid boolean (true/false)");
                    }
                    break;

                case "json":
                    try
                    {
                        JsonDocument.Parse(value);
                    }
                    catch (JsonException ex)
                    {
                        errors.Add($"Value is not valid JSON: {ex.Message}");
                    }
                    break;

                case "string":
                    // Strings are always valid
                    break;

                default:
                    errors.Add($"Unknown value type: {valueType}");
                    break;
            }

            // Additional domain-specific validations
            if (key.StartsWith("RateLimit:") && valueType.ToLower() == "int")
            {
                if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var intValue) && intValue < 0)
                {
                    errors.Add("Rate limit values must be non-negative");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Validation error: {ex.Message}");
        }

        return new ConfigurationValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors
        );
    }

    public async Task<ConfigurationExportDto> ExportConfigurationsAsync(string environment, bool activeOnly = true)
    {
        var query = _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .Include(c => c.UpdatedBy)
            .AsNoTracking();

        query = query.Where(c => c.Environment == environment || c.Environment == "All");

        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        var configurations = await query
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Key)
            .ToListAsync();

        _logger.LogInformation(
            "Exported {Count} configurations for environment {Environment}",
            configurations.Count, environment);

        return new ConfigurationExportDto(
            Configurations: configurations.Select(MapToDto).ToList(),
            ExportedAt: _timeProvider.GetUtcNow().UtcDateTime,
            Environment: environment
        );
    }

    public async Task<int> ImportConfigurationsAsync(ConfigurationImportRequest request, string userId)
    {
        var importedCount = 0;

        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var configRequest in request.Configurations)
            {
                var exists = await _dbContext.SystemConfigurations
                    .AnyAsync(c => c.Key == configRequest.Key && c.Environment == configRequest.Environment);

                if (exists)
                {
                    if (request.OverwriteExisting)
                    {
                        // Update existing
                        var entity = await _dbContext.SystemConfigurations
                            .FirstAsync(c => c.Key == configRequest.Key && c.Environment == configRequest.Environment);

                        entity.PreviousValue = entity.Value;
                        entity.Value = configRequest.Value;
                        entity.ValueType = configRequest.ValueType;
                        entity.Description = configRequest.Description;
                        entity.Category = configRequest.Category;
                        entity.IsActive = configRequest.IsActive;
                        entity.RequiresRestart = configRequest.RequiresRestart;
                        entity.Version++;
                        entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                        entity.UpdatedByUserId = userId;

                        importedCount++;
                    }
                    // else skip existing
                }
                else
                {
                    // Create new
                    var entity = new SystemConfigurationEntity
                    {
                        Id = Guid.NewGuid().ToString(),
                        Key = configRequest.Key,
                        Value = configRequest.Value,
                        ValueType = configRequest.ValueType,
                        Description = configRequest.Description,
                        Category = configRequest.Category,
                        IsActive = configRequest.IsActive,
                        RequiresRestart = configRequest.RequiresRestart,
                        Environment = configRequest.Environment,
                        Version = 1,
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                        UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                        CreatedByUserId = userId
                    };

                    _dbContext.SystemConfigurations.Add(entity);
                    importedCount++;
                }
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Imported {Count} configurations by user {UserId}",
                importedCount, userId);

            // Invalidate all caches
            await InvalidateCacheAsync();

            return importedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Configuration import failed for user {UserId}. Attempting rollback. Total configs: {TotalCount}, Imported before failure: {ImportedCount}",
                userId,
                request.Configurations.Count,
                importedCount);

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback transaction after import error for user {UserId}",
                    userId);
                // Don't throw rollback exception - original exception is more important
            }

            throw; // Re-throw original exception
        }
    }

    public async Task<IReadOnlyList<ConfigurationHistoryDto>> GetConfigurationHistoryAsync(
        string configurationId,
        int limit = 20)
    {
        // For now, we track history through the version field and previous_value
        // A full audit trail would require a separate configuration_history table
        // This is a simplified implementation that returns the current and previous version

        var config = await _dbContext.SystemConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == configurationId);

        if (config == null)
        {
            return Array.Empty<ConfigurationHistoryDto>();
        }

        var history = new List<ConfigurationHistoryDto>();

        // Current version
        history.Add(new ConfigurationHistoryDto(
            Id: Guid.NewGuid().ToString(),
            ConfigurationId: config.Id,
            Key: config.Key,
            OldValue: config.PreviousValue ?? "",
            NewValue: config.Value,
            Version: config.Version,
            ChangedAt: config.UpdatedAt,
            ChangedByUserId: config.UpdatedByUserId ?? config.CreatedByUserId ?? "system",
            ChangeReason: "Configuration updated"
        ));

        return history;
    }

    public async Task<SystemConfigurationDto?> RollbackConfigurationAsync(
        string configurationId,
        int toVersion,
        string userId)
    {
        var entity = await _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == configurationId);

        if (entity == null)
        {
            return null;
        }

        if (entity.Version == toVersion)
        {
            _logger.LogWarning(
                "Configuration {Key} is already at version {Version}",
                entity.Key, toVersion);
            return MapToDto(entity);
        }

        // For simplified implementation, rollback to previous value
        if (!string.IsNullOrEmpty(entity.PreviousValue))
        {
            var currentValue = entity.Value;
            entity.Value = entity.PreviousValue;
            entity.PreviousValue = currentValue;
            entity.Version++;
            entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            entity.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Configuration {Key} rolled back to previous value by user {UserId}",
                entity.Key, userId);

            // Invalidate cache
            await InvalidateCacheAsync(entity.Key);

            // Load navigation properties
            await _dbContext.Entry(entity).Reference(e => e.UpdatedBy).LoadAsync();

            return MapToDto(entity);
        }

        throw new InvalidOperationException("No previous value available for rollback");
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        var categories = await _dbContext.SystemConfigurations
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return categories;
    }

    public async Task InvalidateCacheAsync()
    {
        _logger.LogInformation("Invalidating all configuration cache entries");

        // Remove all cache entries with config: prefix using tag-based invalidation
        await _cache.RemoveByTagAsync(CacheCategoryTagPrefix + "general");
    }

    public async Task InvalidateCacheAsync(string key)
    {
        _logger.LogDebug("Invalidating cache for configuration key: {Key}", key);

        // Invalidate for all environments
        var environments = new[] { "Development", "Staging", "Production", "All" };
        foreach (var env in environments)
        {
            var cacheKey = GetCacheKey(key, env);
            await _cache.RemoveAsync(cacheKey);
        }
    }

    // Helper methods

    private static string GetCacheKey(string key, string environment)
    {
        return $"{CacheKeyPrefix}{key}:{environment}";
    }

    private static SystemConfigurationDto MapToDto(SystemConfigurationEntity entity)
    {
        return new SystemConfigurationDto(
            Id: entity.Id,
            Key: entity.Key,
            Value: entity.Value,
            ValueType: entity.ValueType,
            Description: entity.Description,
            Category: entity.Category,
            IsActive: entity.IsActive,
            RequiresRestart: entity.RequiresRestart,
            Environment: entity.Environment,
            Version: entity.Version,
            PreviousValue: entity.PreviousValue,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt,
            CreatedByUserId: entity.CreatedByUserId,
            UpdatedByUserId: entity.UpdatedByUserId,
            LastToggledAt: entity.LastToggledAt
        );
    }

    private static T? DeserializeValue<T>(string value, string valueType)
    {
        return valueType.ToLower() switch
        {
            "int" or "integer" => (T)(object)int.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "long" => (T)(object)long.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "double" or "float" => (T)(object)double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "bool" or "boolean" => (T)(object)bool.Parse(value),
            "json" => JsonSerializer.Deserialize<T>(value),
            "string" => (T)(object)value,
            _ => throw new InvalidOperationException($"Unsupported value type: {valueType}")
        };
    }
}
