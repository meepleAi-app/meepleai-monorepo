// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;

/// <summary>
/// Abstract base class for RAG plugins providing common functionality.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class to implement a RAG plugin with built-in:
/// - Input/output validation
/// - Execution timing and metrics
/// - Error handling and logging
/// - Health check infrastructure
/// </para>
/// <para>
/// Derived classes must implement:
/// - <see cref="ExecuteCoreAsync"/> for the main plugin logic
/// - <see cref="CreateInputSchema"/>, <see cref="CreateOutputSchema"/>, <see cref="CreateConfigSchema"/> for schemas
/// </para>
/// </remarks>
public abstract class RagPluginBase : IRagPlugin
{
    private readonly Lazy<JsonDocument> _inputSchema;
    private readonly Lazy<JsonDocument> _outputSchema;
    private readonly Lazy<JsonDocument> _configSchema;
    private readonly Lazy<PluginMetadata> _metadata;

    /// <summary>
    /// Logger for the plugin.
    /// </summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Version { get; }

    /// <inheritdoc />
    public abstract PluginCategory Category { get; }

    /// <inheritdoc />
    public JsonDocument InputSchema => _inputSchema.Value;

    /// <inheritdoc />
    public JsonDocument OutputSchema => _outputSchema.Value;

    /// <inheritdoc />
    public JsonDocument ConfigSchema => _configSchema.Value;

    /// <inheritdoc />
    public PluginMetadata Metadata => _metadata.Value;

    /// <summary>
    /// Optional description for the plugin.
    /// </summary>
    protected virtual string Description => string.Empty;

    /// <summary>
    /// Optional author for the plugin.
    /// </summary>
    protected virtual string Author => "MeepleAI";

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    protected virtual IReadOnlyList<string> Tags => [];

    /// <summary>
    /// Plugin capabilities.
    /// </summary>
    protected virtual IReadOnlyList<string> Capabilities => [];

    /// <summary>
    /// Initializes a new instance of the plugin base class.
    /// </summary>
    /// <param name="logger">Logger instance for the plugin.</param>
    protected RagPluginBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _inputSchema = new Lazy<JsonDocument>(CreateInputSchema);
        _outputSchema = new Lazy<JsonDocument>(CreateOutputSchema);
        _configSchema = new Lazy<JsonDocument>(CreateConfigSchema);
        _metadata = new Lazy<PluginMetadata>(CreateMetadata);
    }

    /// <inheritdoc />
    public async Task<PluginOutput> ExecuteAsync(
        PluginInput input,
        PluginConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        config ??= PluginConfig.Default();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate input
            var inputValidation = ValidateInput(input);
            if (!inputValidation.IsValid)
            {
                Logger.LogWarning(
                    "Plugin {PluginId} input validation failed: {Errors}",
                    Id,
                    string.Join("; ", inputValidation.Errors.Select(e => e.Message)));

                return PluginOutput.Failed(
                    input.ExecutionId,
                    $"Input validation failed: {string.Join("; ", inputValidation.Errors.Select(e => e.Message))}",
                    "VALIDATION_ERROR");
            }

            // Validate config
            var configValidation = ValidateConfig(config);
            if (!configValidation.IsValid)
            {
                Logger.LogWarning(
                    "Plugin {PluginId} config validation failed: {Errors}",
                    Id,
                    string.Join("; ", configValidation.Errors.Select(e => e.Message)));

                return PluginOutput.Failed(
                    input.ExecutionId,
                    $"Config validation failed: {string.Join("; ", configValidation.Errors.Select(e => e.Message))}",
                    "CONFIG_ERROR");
            }

            // Execute with timeout
            using var timeoutCts = new CancellationTokenSource(config.TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = await ExecuteCoreAsync(input, config, linkedCts.Token).ConfigureAwait(false);

            stopwatch.Stop();

            // Enrich with metrics
            return result with
            {
                Metrics = result.Metrics with { DurationMs = stopwatch.Elapsed.TotalMilliseconds }
            };
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            Logger.LogInformation(ex, "Plugin {PluginId} execution cancelled", Id);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            Logger.LogWarning(ex, "Plugin {PluginId} execution timed out after {TimeoutMs}ms", Id, config.TimeoutMs);

            return PluginOutput.Failed(
                input.ExecutionId,
                $"Execution timed out after {config.TimeoutMs}ms",
                "TIMEOUT");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Plugin {PluginId} execution failed", Id);

            return PluginOutput.Failed(
                input.ExecutionId,
                ex.Message,
                "EXECUTION_ERROR");
        }
    }

    /// <inheritdoc />
    public virtual async Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await PerformHealthCheckAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return result with { CheckDurationMs = stopwatch.Elapsed.TotalMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Plugin {PluginId} health check failed", Id);

            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Message = $"Health check failed: {ex.Message}",
                CheckDurationMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public virtual ValidationResult ValidateConfig(PluginConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<ValidationError>();

        if (config.TimeoutMs <= 0)
        {
            errors.Add(new ValidationError
            {
                Message = "Timeout must be greater than 0",
                PropertyPath = "TimeoutMs",
                Code = "INVALID_TIMEOUT",
                AttemptedValue = config.TimeoutMs
            });
        }

        if (config.MaxRetries < 0)
        {
            errors.Add(new ValidationError
            {
                Message = "MaxRetries cannot be negative",
                PropertyPath = "MaxRetries",
                Code = "INVALID_RETRIES",
                AttemptedValue = config.MaxRetries
            });
        }

        if (config.RetryBackoffMs < 0)
        {
            errors.Add(new ValidationError
            {
                Message = "RetryBackoffMs cannot be negative",
                PropertyPath = "RetryBackoffMs",
                Code = "INVALID_BACKOFF",
                AttemptedValue = config.RetryBackoffMs
            });
        }

        // Allow derived classes to add custom validation
        var customValidation = ValidateConfigCore(config);
        if (!customValidation.IsValid)
        {
            errors.AddRange(customValidation.Errors);
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors.ToArray());
    }

    /// <inheritdoc />
    public virtual ValidationResult ValidateInput(PluginInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<ValidationError>();

        if (input.ExecutionId == Guid.Empty)
        {
            errors.Add(new ValidationError
            {
                Message = "ExecutionId is required",
                PropertyPath = "ExecutionId",
                Code = "MISSING_EXECUTION_ID"
            });
        }

        if (input.Payload == null)
        {
            errors.Add(new ValidationError
            {
                Message = "Payload is required",
                PropertyPath = "Payload",
                Code = "MISSING_PAYLOAD"
            });
        }

        // Allow derived classes to add custom validation
        var customValidation = ValidateInputCore(input);
        if (!customValidation.IsValid)
        {
            errors.AddRange(customValidation.Errors);
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors.ToArray());
    }

    /// <summary>
    /// Core execution logic to be implemented by derived classes.
    /// </summary>
    protected abstract Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates the JSON Schema for input validation.
    /// </summary>
    protected abstract JsonDocument CreateInputSchema();

    /// <summary>
    /// Creates the JSON Schema for output documentation.
    /// </summary>
    protected abstract JsonDocument CreateOutputSchema();

    /// <summary>
    /// Creates the JSON Schema for configuration validation.
    /// </summary>
    protected abstract JsonDocument CreateConfigSchema();

    /// <summary>
    /// Performs the actual health check. Override to add custom checks.
    /// </summary>
    protected virtual Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <summary>
    /// Custom configuration validation. Override to add plugin-specific validation.
    /// </summary>
    protected virtual ValidationResult ValidateConfigCore(PluginConfig config)
    {
        return ValidationResult.Success();
    }

    /// <summary>
    /// Custom input validation. Override to add plugin-specific validation.
    /// </summary>
    protected virtual ValidationResult ValidateInputCore(PluginInput input)
    {
        return ValidationResult.Success();
    }

    /// <summary>
    /// Creates the plugin metadata.
    /// </summary>
    private PluginMetadata CreateMetadata()
    {
        return new PluginMetadata
        {
            Id = Id,
            Name = Name,
            Version = Version,
            Category = Category,
            Description = Description,
            Author = Author,
            Tags = Tags,
            Capabilities = Capabilities,
            IsEnabled = true,
            IsBuiltIn = true,
            RegisteredAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Helper to create a basic JSON Schema document.
    /// </summary>
    protected static JsonDocument CreateBasicSchema(string type, string description)
    {
        var schema = $$"""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "{{type}}",
            "description": "{{description}}"
        }
        """;

        return JsonDocument.Parse(schema);
    }

    /// <summary>
    /// Helper to create a JSON Schema from a JSON string.
    /// </summary>
    protected static JsonDocument CreateSchemaFromJson(string json)
    {
        return JsonDocument.Parse(json);
    }
}
