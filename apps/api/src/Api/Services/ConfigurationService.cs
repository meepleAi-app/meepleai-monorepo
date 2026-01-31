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
internal class ConfigurationService : IConfigurationService
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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Configuration deserialization failures must not crash application;
        // return default value to allow graceful degradation.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to deserialize configuration {Key} of type {ValueType}", key, config.ValueType);
            return defaultValue;
        }
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
        var cachedValue = await _cache.GetOrCreateAsync<SystemConfigurationDto?>(
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
        ).ConfigureAwait(false);
#pragma warning restore CS8621
#pragma warning restore CS8634

        return cachedValue;
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
