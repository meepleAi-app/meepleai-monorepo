using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using MediatR;

namespace Api.Services;

/// <summary>
/// Infrastructure service for typed configuration value retrieval.
/// Handles caching, deserialization, and environment-specific fallback.
/// All CRUD operations now handled by CQRS commands/queries in SystemConfiguration bounded context.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IMediator _mediator;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string CacheKeyPrefix = "config:";
    private const string CacheCategoryTagPrefix = "config:category:";
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);

    public ConfigurationService(
        IMediator mediator,
        IHybridCacheService cache,
        ILogger<ConfigurationService> logger,
        IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _cache = cache;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Get a typed configuration value by key with caching and environment fallback.
    /// This is the primary infrastructure method used by other services.
    /// </summary>
    public async Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null)
    {
        var config = await GetConfigurationByKeyAsync(key, environment).ConfigureAwait(false);

        if (config == null)
        {
            _logger.LogDebug("Configuration {Key} not found, returning default value", key);
            return defaultValue;
        }

        try
        {
            return DeserializeValue<T>(config.Value, config.ValueType);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize configuration {Key} of type {ValueType}", key, config.ValueType);
            return defaultValue;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Get a configuration by key with environment-specific fallback and caching.
    /// Supports infrastructure needs for typed value retrieval.
    /// </summary>
    public async Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        var currentEnvironment = environment ?? _environment.EnvironmentName;
        var cacheKey = GetCacheKey(key, currentEnvironment);

#pragma warning disable CS8634
#pragma warning disable CS8621
        var cachedValue = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                // Use CQRS query to retrieve configuration with environment and active filters
                var query = new GetConfigByKeyQuery(key, currentEnvironment, ActiveOnly: true);
                var config = await _mediator.Send(query, cancel).ConfigureAwait(false);
                return config != null ? MapFromDto(config) : null;
            },
            tags: [CacheCategoryTagPrefix + "general"],
            expiration: DefaultCacheDuration,
            ct: cancellationToken
        );
#pragma warning restore CS8621
#pragma warning restore CS8634

        return cachedValue;
    }

    // ========================================
    // CRUD Operations - Delegated to CQRS
    // ========================================
    // Note: The following methods delegate to CQRS commands/queries.
    // They are kept for backward compatibility but should eventually be removed
    // as consumers migrate to using MediatR directly.

    public async Task<PagedResult<SystemConfigurationDto>> GetConfigurationsAsync(
        string? category = null,
        string? environment = null,
        bool activeOnly = true,
        int page = 1,
        int pageSize = 50)
    {
        var query = new GetAllConfigsQuery(category, environment, activeOnly, page, pageSize);
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return new PagedResult<SystemConfigurationDto>(
            Items: result.Items.Select(MapFromDto).ToList(),
            Total: result.Total,
            Page: page,
            PageSize: pageSize
        );
    }

    public async Task<SystemConfigurationDto?> GetConfigurationByIdAsync(Guid id)
    {
        var query = new GetConfigByIdQuery(id);
        var result = await _mediator.Send(query).ConfigureAwait(false);
        return result != null ? MapFromDto(result) : null;
    }

    public async Task<SystemConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, Guid userId)
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.CreateConfigurationCommand(
            Key: request.Key,
            Value: request.Value,
            ValueType: request.ValueType,
            CreatedByUserId: userId,
            Description: request.Description,
            Category: request.Category,
            Environment: request.Environment,
            RequiresRestart: request.RequiresRestart
        );
        var result = await _mediator.Send(command).ConfigureAwait(false);
        return MapFromDto(result);
    }

    public async Task<SystemConfigurationDto?> UpdateConfigurationAsync(Guid id, UpdateConfigurationRequest request, Guid userId)
    {
        if (request.Value == null)
            throw new InvalidOperationException("Value is required for update");

        var command = new BoundedContexts.SystemConfiguration.Application.Commands.UpdateConfigValueCommand(
            ConfigId: id,
            NewValue: request.Value,
            UpdatedByUserId: userId
        );
        var result = await _mediator.Send(command).ConfigureAwait(false);
        return result != null ? MapFromDto(result) : null;
    }

    public async Task<bool> DeleteConfigurationAsync(Guid id)
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.DeleteConfigurationCommand(id);
        return await _mediator.Send(command).ConfigureAwait(false);
    }

    public async Task<SystemConfigurationDto?> ToggleConfigurationAsync(Guid id, bool isActive, Guid userId)
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.ToggleConfigurationCommand(id, isActive);
        var result = await _mediator.Send(command).ConfigureAwait(false);
        return result != null ? MapFromDto(result) : null;
    }

    public async Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync(
        BulkConfigurationUpdateRequest request,
        Guid userId)
    {
        var updates = request.Updates.Select(u => new BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationUpdate(
            Id: Guid.Parse(u.Id),
            Value: u.Value
        )).ToList();

        var command = new BoundedContexts.SystemConfiguration.Application.Commands.BulkUpdateConfigsCommand(updates, userId);
        var result = await _mediator.Send(command).ConfigureAwait(false);
        return result.Select(MapFromDto).ToList();
    }

    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(string key, string value, string valueType)
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.ValidateConfigCommand(key, value, valueType);
        var result = await _mediator.Send(command).ConfigureAwait(false);
        return new ConfigurationValidationResult(result.IsValid, result.Errors);
    }

    public async Task<Models.ConfigurationExportDto> ExportConfigurationsAsync(string environment, bool activeOnly = true)
    {
        var query = new ExportConfigsQuery(environment, activeOnly);
        var result = await _mediator.Send(query).ConfigureAwait(false);

        return new Models.ConfigurationExportDto(
            Configurations: result.Configurations.Select(MapFromDto).ToList(),
            ExportedAt: result.ExportedAt,
            Environment: result.Environment
        );
    }

    public async Task<int> ImportConfigurationsAsync(ConfigurationImportRequest request, Guid userId)
    {
        var items = request.Configurations.Select(c => new BoundedContexts.SystemConfiguration.Application.Commands.ConfigurationImportItem(
            Key: c.Key,
            Value: c.Value,
            ValueType: c.ValueType,
            Description: c.Description,
            Category: c.Category,
            IsActive: c.IsActive,
            RequiresRestart: c.RequiresRestart,
            Environment: c.Environment
        )).ToList();

        var command = new BoundedContexts.SystemConfiguration.Application.Commands.ImportConfigsCommand(items, request.OverwriteExisting, userId);
        return await _mediator.Send(command).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Models.ConfigurationHistoryDto>> GetConfigurationHistoryAsync(Guid configurationId, int limit = 20)
    {
        var query = new GetConfigHistoryQuery(configurationId, limit);
        var result = await _mediator.Send(query).ConfigureAwait(false);
        return result.Select(h => new Models.ConfigurationHistoryDto(
            Id: h.Id,
            ConfigurationId: h.ConfigurationId,
            Key: h.Key,
            OldValue: h.OldValue,
            NewValue: h.NewValue,
            Version: h.Version,
            ChangedAt: h.ChangedAt,
            ChangedByUserId: h.ChangedByUserId,
            ChangeReason: h.ChangeReason
        )).ToList();
    }

    public async Task<SystemConfigurationDto?> RollbackConfigurationAsync(Guid configurationId, int toVersion, Guid userId)
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.RollbackConfigCommand(configurationId, toVersion, userId);
        var result = await _mediator.Send(command).ConfigureAwait(false);
        return result != null ? MapFromDto(result) : null;
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        var query = new GetConfigCategoriesQuery();
        return await _mediator.Send(query).ConfigureAwait(false);
    }

    public async Task InvalidateCacheAsync()
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.InvalidateCacheCommand(null);
        await _mediator.Send(command).ConfigureAwait(false);
    }

    public async Task InvalidateCacheAsync(string key)
    {
        var command = new BoundedContexts.SystemConfiguration.Application.Commands.InvalidateCacheCommand(key);
        await _mediator.Send(command).ConfigureAwait(false);
    }

    // ========================================
    // Private Helper Methods
    // ========================================

    private static string GetCacheKey(string key, string environment)
    {
        return $"{CacheKeyPrefix}{key}:{environment}";
    }

    private static SystemConfigurationDto MapFromDto(ConfigurationDto dto)
    {
        return new SystemConfigurationDto(
            Id: dto.Id.ToString(),
            Key: dto.Key,
            Value: dto.Value,
            ValueType: dto.ValueType,
            Description: dto.Description,
            Category: dto.Category,
            IsActive: dto.IsActive,
            RequiresRestart: dto.RequiresRestart,
            Environment: dto.Environment,
            Version: dto.Version,
            PreviousValue: null,
            CreatedAt: dto.CreatedAt,
            UpdatedAt: dto.UpdatedAt,
            CreatedByUserId: Guid.Empty.ToString(),
            UpdatedByUserId: null,
            LastToggledAt: null
        );
    }

    private static T? DeserializeValue<T>(string value, string valueType)
    {
        // Parse to intermediate type, then convert to target type
        object parsed = valueType.ToLower(System.Globalization.CultureInfo.InvariantCulture) switch
        {
            "int" or "integer" => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "long" => long.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "double" or "float" or "decimal" => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "bool" or "boolean" => bool.Parse(value),
            "json" => JsonSerializer.Deserialize<T>(value)!,
            "string" => value,
            _ => throw new InvalidOperationException($"Unsupported value type: {valueType}")
        };

        // Handle JSON deserialization directly
        if (parsed is T typedValue)
            return typedValue;

        // Convert to target type (handles decimal/double conversions properly)
        return (T)Convert.ChangeType(parsed, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}