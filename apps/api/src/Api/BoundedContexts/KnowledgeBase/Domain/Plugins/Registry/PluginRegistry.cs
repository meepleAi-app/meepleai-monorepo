// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3417 - Plugin Registry Service
// =============================================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Registry;

/// <summary>
/// Default implementation of the plugin registry.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginRegistry> _logger;
    private readonly PluginRegistryOptions _options;

    private readonly ConcurrentDictionary<string, PluginRegistration> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _enabledState = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HealthCheckResult> _healthCache = new(StringComparer.OrdinalIgnoreCase);

    // Thread-safe lazy initialization pattern (Issue #async-antipattern)
    // Lazy<Task> ensures InitializeAsync runs once, safely handles concurrent access
    private Lazy<Task> _initialization;

    // Separate lock for RefreshAsync to support reinitialization
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Creates a new PluginRegistry.
    /// </summary>
    public PluginRegistry(
        IServiceProvider serviceProvider,
        ILogger<PluginRegistry> logger,
        IOptions<PluginRegistryOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new PluginRegistryOptions();

        // Initialize lazy async task for thread-safe initialization
        _initialization = new Lazy<Task>(() => InitializeInternalAsync(CancellationToken.None));
    }

    #region Discovery

    /// <inheritdoc/>
    /// <remarks>
    /// WARNING: This sync method blocks on first call. Prefer async methods for production code.
    /// </remarks>
    public IReadOnlyList<PluginMetadata> GetAllPlugins()
    {
        // SYNC METHOD: Blocks thread if not yet initialized (first call only)
        if (!_initialization.IsValueCreated)
        {
            _initialization.Value.GetAwaiter().GetResult();
        }
        return _plugins.Values
            .Select(r => r.Metadata)
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<PluginMetadata> GetPluginsByCategory(PluginCategory category)
    {
        // Sync method: blocks if not yet initialized
        if (!_initialization.IsValueCreated)
        {
            _initialization.Value.GetAwaiter().GetResult();
        }
        return _plugins.Values
            .Where(r => r.Metadata.Category == category)
            .Select(r => r.Metadata)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public PluginMetadata? GetPlugin(string pluginId, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        // Sync method: blocks if not yet initialized
        if (!_initialization.IsValueCreated)
        {
            _initialization.Value.GetAwaiter().GetResult();
        }

        var key = CreateKey(pluginId, version);
        if (_plugins.TryGetValue(key, out var registration))
        {
            return registration.Metadata;
        }

        // If version not specified, try to find latest
        if (version == null)
        {
            var latestKey = _plugins.Keys
                .Where(k => k.StartsWith(pluginId + ":", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(k => k, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (latestKey != null && _plugins.TryGetValue(latestKey, out registration))
            {
                return registration.Metadata;
            }

            // Try without version suffix
            if (_plugins.TryGetValue(pluginId, out registration))
            {
                return registration.Metadata;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsRegistered(string pluginId, string? version = null)
    {
        return GetPlugin(pluginId, version) != null;
    }

    #endregion

    #region Loading

    /// <inheritdoc/>
    public IRagPlugin LoadPlugin(string pluginId, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        // Sync method: blocks if not yet initialized
        if (!_initialization.IsValueCreated)
        {
            _initialization.Value.GetAwaiter().GetResult();
        }

        var registration = GetRegistration(pluginId, version)
            ?? throw new PluginNotFoundException(pluginId, version);

        // Use the registration's actual ID and version for consistent key lookup
        var key = CreateKey(registration.Metadata.Id, registration.Metadata.Version);
        if (_enabledState.TryGetValue(key, out var isEnabled) && !isEnabled)
        {
            throw new PluginDisabledException(pluginId);
        }

        if (!registration.Metadata.IsEnabled)
        {
            throw new PluginDisabledException(pluginId);
        }

        try
        {
            // Create instance via DI
            var plugin = (IRagPlugin)_serviceProvider.GetRequiredService(registration.PluginType);
            _logger.LogDebug("Loaded plugin {PluginId} version {Version}", pluginId, registration.Metadata.Version);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin {PluginId}", pluginId);
            throw new PluginRegistrationException(
                $"Failed to load plugin '{pluginId}': {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc/>
    public bool TryLoadPlugin(string pluginId, out IRagPlugin? plugin, string? version = null)
    {
        try
        {
            plugin = LoadPlugin(pluginId, version);
            return true;
        }
        catch
        {
            plugin = null;
            return false;
        }
    }

    #endregion

    #region Health

    /// <inheritdoc/>
    public async Task<PluginHealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var entries = new Dictionary<string, PluginHealthEntry>(StringComparer.OrdinalIgnoreCase);

        var healthyCount = 0;
        var degradedCount = 0;
        var unhealthyCount = 0;
        var disabledCount = 0;

        foreach (var registration in _plugins.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = CreateKey(registration.Metadata.Id, registration.Metadata.Version);
            var isEnabled = !_enabledState.TryGetValue(key, out var enabled) || enabled;

            if (!isEnabled || !registration.Metadata.IsEnabled)
            {
                disabledCount++;
                entries[registration.Metadata.Id] = new PluginHealthEntry
                {
                    Metadata = registration.Metadata,
                    HealthResult = HealthCheckResult.Unknown("Plugin is disabled"),
                    IsEnabled = false
                };
                continue;
            }

            var healthResult = await CheckPluginHealthInternalAsync(registration, cancellationToken).ConfigureAwait(false);

            entries[registration.Metadata.Id] = new PluginHealthEntry
            {
                Metadata = registration.Metadata,
                HealthResult = healthResult,
                IsEnabled = true
            };

            switch (healthResult.Status)
            {
                case HealthStatus.Healthy:
                    healthyCount++;
                    break;
                case HealthStatus.Degraded:
                    degradedCount++;
                    break;
                case HealthStatus.Unhealthy:
                    unhealthyCount++;
                    break;
            }
        }

        stopwatch.Stop();

        var overallStatus = unhealthyCount > 0 ? HealthStatus.Unhealthy
            : degradedCount > 0 ? HealthStatus.Degraded
            : HealthStatus.Healthy;

        return new PluginHealthReport
        {
            OverallStatus = overallStatus,
            TotalPlugins = _plugins.Count,
            HealthyCount = healthyCount,
            DegradedCount = degradedCount,
            UnhealthyCount = unhealthyCount,
            DisabledCount = disabledCount,
            Plugins = entries,
            ReportDurationMs = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckPluginHealthAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        await EnsureInitializedAsync().ConfigureAwait(false);

        var registration = GetRegistration(pluginId, null);
        if (registration == null)
        {
            return HealthCheckResult.Unknown($"Plugin '{pluginId}' not found");
        }

        return await CheckPluginHealthInternalAsync(registration, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HealthCheckResult> CheckPluginHealthInternalAsync(
        PluginRegistration registration,
        CancellationToken cancellationToken)
    {
        var cacheKey = CreateKey(registration.Metadata.Id, registration.Metadata.Version);

        // Check cache if not expired
        if (_healthCache.TryGetValue(cacheKey, out var cached) &&
            (DateTimeOffset.UtcNow - cached.CheckedAt).TotalSeconds < _options.HealthCacheSeconds)
        {
            return cached;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Try to load and health check the plugin
            var plugin = (IRagPlugin)_serviceProvider.GetRequiredService(registration.PluginType);
            var result = await plugin.HealthCheckAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            var healthResult = new HealthCheckResult
            {
                Status = result.Status,
                Message = result.Message,
                CheckDurationMs = stopwatch.Elapsed.TotalMilliseconds,
                CheckedAt = DateTimeOffset.UtcNow,
                Components = result.Components,
                Diagnostics = result.Diagnostics
            };

            _healthCache[cacheKey] = healthResult;
            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for plugin {PluginId}", registration.Metadata.Id);

            var unhealthyResult = HealthCheckResult.Unhealthy($"Health check failed: {ex.Message}");
            _healthCache[cacheKey] = unhealthyResult;
            return unhealthyResult;
        }
    }

    #endregion

    #region Management

    /// <inheritdoc/>
    public async Task EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        await EnsureInitializedAsync().ConfigureAwait(false);

        var registration = GetRegistration(pluginId, null)
            ?? throw new PluginNotFoundException(pluginId);

        var key = CreateKey(registration.Metadata.Id, registration.Metadata.Version);
        _enabledState[key] = true;

        _logger.LogInformation("Enabled plugin {PluginId}", pluginId);
    }

    /// <inheritdoc/>
    public async Task DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        await EnsureInitializedAsync().ConfigureAwait(false);

        var registration = GetRegistration(pluginId, null)
            ?? throw new PluginNotFoundException(pluginId);

        var key = CreateKey(registration.Metadata.Id, registration.Metadata.Version);
        _enabledState[key] = false;

        _logger.LogInformation("Disabled plugin {PluginId}", pluginId);
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _plugins.Clear();
            _healthCache.Clear();

            // Recreate Lazy<Task> for new initialization
            _initialization = new Lazy<Task>(() => InitializeInternalAsync(CancellationToken.None));

            // Trigger initialization immediately
            await _initialization.Value.ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Ensures plugin registry is initialized asynchronously.
    /// Thread-safe via Lazy pattern - initialization runs once.
    /// </summary>
    private Task EnsureInitializedAsync() => _initialization.Value;

    /// <summary>
    /// Internal initialization logic called once by Lazy pattern.
    /// Discovers and registers all plugins from configured assemblies.
    /// </summary>
    private Task InitializeInternalAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing plugin registry...");

        var assemblies = GetAssembliesToScan();
        var pluginTypes = DiscoverPluginTypes(assemblies);

        foreach (var pluginType in pluginTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RegisterPlugin(pluginType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register plugin type {PluginType}", pluginType.FullName);
            }
        }

        _logger.LogInformation("Plugin registry initialized with {PluginCount} plugins", _plugins.Count);

        return Task.CompletedTask;
    }

    private IEnumerable<Assembly> GetAssembliesToScan()
    {
        var assemblies = new List<Assembly>();

        // Always scan the current assembly
        assemblies.Add(GetType().Assembly);

        // Scan configured assemblies
        foreach (var assemblyName in _options.AssembliesToScan)
        {
            try
            {
                var assembly = Assembly.Load(assemblyName);
                assemblies.Add(assembly);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load assembly {AssemblyName}", assemblyName);
            }
        }

        return assemblies.Distinct();
    }

    private static IEnumerable<Type> DiscoverPluginTypes(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null).Cast<Type>();
                }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetCustomAttribute<RagPluginAttribute>() != null)
            .Where(t => typeof(IRagPlugin).IsAssignableFrom(t));
    }

    private void RegisterPlugin(Type pluginType)
    {
        var attribute = pluginType.GetCustomAttribute<RagPluginAttribute>()
            ?? throw new PluginRegistrationException(pluginType, "Missing RagPluginAttribute");

        var metadata = new PluginMetadata
        {
            Id = attribute.Id,
            Name = attribute.Name ?? pluginType.Name.Replace("Plugin", "", StringComparison.Ordinal),
            Version = attribute.Version,
            Category = attribute.Category,
            Description = attribute.Description ?? string.Empty,
            Author = attribute.Author ?? string.Empty,
            IsEnabled = true,
            IsBuiltIn = attribute.IsBuiltIn,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        var registration = new PluginRegistration(metadata, pluginType);
        var key = CreateKey(metadata.Id, metadata.Version);

        if (!_plugins.TryAdd(key, registration))
        {
            _logger.LogWarning("Plugin {PluginId} version {Version} is already registered", metadata.Id, metadata.Version);
            return;
        }

        // Also register without version for latest lookup
        _plugins.TryAdd(metadata.Id, registration);

        _logger.LogDebug("Registered plugin {PluginId} version {Version} ({Category})",
            metadata.Id, metadata.Version, metadata.Category);
    }

    private PluginRegistration? GetRegistration(string pluginId, string? version)
    {
        var key = CreateKey(pluginId, version);

        if (_plugins.TryGetValue(key, out var registration))
        {
            return registration;
        }

        // If version not specified, try to find latest
        if (version == null)
        {
            var latestKey = _plugins.Keys
                .Where(k => k.StartsWith(pluginId + ":", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(k => k, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (latestKey != null && _plugins.TryGetValue(latestKey, out registration))
            {
                return registration;
            }

            // Try without version suffix
            if (_plugins.TryGetValue(pluginId, out registration))
            {
                return registration;
            }
        }

        return null;
    }

    private static string CreateKey(string pluginId, string? version)
    {
        return version != null ? $"{pluginId}:{version}" : pluginId;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the plugin registry and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _refreshLock.Dispose();
        _disposed = true;
    }

    #endregion

    /// <summary>
    /// Internal registration record.
    /// </summary>
    private sealed record PluginRegistration(PluginMetadata Metadata, Type PluginType);
}

/// <summary>
/// Configuration options for the plugin registry.
/// </summary>
public sealed class PluginRegistryOptions
{
    /// <summary>
    /// Assembly names to scan for plugins.
    /// </summary>
    public IList<string> AssembliesToScan { get; set; } = new List<string>();

    /// <summary>
    /// Whether to auto-register discovered plugins with DI.
    /// </summary>
    public bool AutoRegisterWithDI { get; set; } = true;

    /// <summary>
    /// Health check cache duration in seconds.
    /// </summary>
    public int HealthCacheSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to throw on duplicate plugin IDs.
    /// </summary>
    public bool ThrowOnDuplicates { get; set; }

    /// <summary>
    /// Timeout for plugin health checks in milliseconds.
    /// </summary>
    public int HealthCheckTimeoutMs { get; set; } = 5000;
}
