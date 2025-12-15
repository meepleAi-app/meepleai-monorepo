using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace Api.Logging;

/// <summary>
/// OPS-04: Centralized logging configuration for environment-based log levels and structured logging.
/// Provides consistent Serilog setup across all environments with appropriate defaults.
/// </summary>
internal static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog with environment-specific settings.
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <returns>Configured LoggerConfiguration</returns>
    public static LoggerConfiguration ConfigureSerilog(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var environment = builder.Environment;

        // Get log level from configuration or use environment-based defaults
        var defaultLogLevel = GetDefaultLogLevel(environment.EnvironmentName, configuration);
        var aspNetCoreLogLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft.AspNetCore", LogEventLevel.Warning);
        var efCoreLogLevel = GetLogLevel(configuration, "Logging:LogLevel:Microsoft.EntityFrameworkCore", LogEventLevel.Warning);

        // Issue #1563: HyperDX OTLP configuration
        var hyperDxLogsEndpoint = configuration["HYPERDX_LOGS_ENDPOINT"] ?? "http://meepleai-hyperdx:14318/v1/logs";

        // Build logger configuration
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(defaultLogLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", aspNetCoreLogLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", efCoreLogLevel)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning) // Reduce HTTP client noise
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "meepleai-api")
            .Enrich.WithProperty("Environment", environment.EnvironmentName);

        // Add sensitive data redaction (objects + scalar strings)
        loggerConfig
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .Enrich.With(new SensitiveStringRedactionEnricher());

        // SEC-731: Add log forging sanitization (removes \r and \n from all strings)
        // This prevents attackers from injecting fake log entries via newlines in user input
        loggerConfig
            .Destructure.With<LogForgingSanitizationPolicy>()
            .Enrich.With<LogForgingSanitizationEnricher>();

        // Console sink with appropriate formatting
        var consoleTemplate = environment.IsDevelopment()
            ? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            : "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

        loggerConfig.WriteTo.Console(
            outputTemplate: consoleTemplate,
            restrictedToMinimumLevel: GetConsoleLogLevel(environment.EnvironmentName));

        // Issue #1563: Add HyperDX OTLP sink (replaces Seq)
        loggerConfig.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = hyperDxLogsEndpoint;
            options.Protocol = OtlpProtocol.HttpProtobuf;
            options.ResourceAttributes = new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                ["service.name"] = "meepleai-api",
                ["deployment.environment"] = environment.EnvironmentName,
                ["service.namespace"] = "meepleai"
            };
            options.RestrictedToMinimumLevel = GetHyperDxLogLevel(environment.EnvironmentName, configuration);
        });

        return loggerConfig;
    }

    /// <summary>
    /// Gets the default log level based on environment.
    /// </summary>
    private static LogEventLevel GetDefaultLogLevel(string environmentName, IConfiguration configuration)
    {
        // Check configuration first
        var configuredLevel = configuration["Logging:LogLevel:Default"];
        if (!string.IsNullOrWhiteSpace(configuredLevel) &&
            Enum.TryParse<LogEventLevel>(configuredLevel, true, out var level))
        {
            return level;
        }

        // Environment-based defaults
        return environmentName.ToLowerInvariant() switch
        {
            "development" => LogEventLevel.Debug,
            "staging" => LogEventLevel.Information,
            "production" => LogEventLevel.Information,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Gets a specific log level from configuration.
    /// </summary>
    private static LogEventLevel GetLogLevel(IConfiguration configuration, string key, LogEventLevel defaultLevel)
    {
        var configuredLevel = configuration[key];
        if (!string.IsNullOrWhiteSpace(configuredLevel) &&
            Enum.TryParse<LogEventLevel>(configuredLevel, true, out var level))
        {
            return level;
        }
        return defaultLevel;
    }

    /// <summary>
    /// Gets the console log level based on environment.
    /// Production may want to reduce console verbosity.
    /// </summary>
    private static LogEventLevel GetConsoleLogLevel(string environmentName)
    {
        return environmentName.ToLowerInvariant() switch
        {
            "development" => LogEventLevel.Debug,
            "staging" => LogEventLevel.Information,
            "production" => LogEventLevel.Warning, // Less verbose in prod
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Gets the HyperDX log level based on environment and configuration.
    /// Issue #1563: All environments should log to HyperDX, but threshold may vary.
    /// </summary>
    private static LogEventLevel GetHyperDxLogLevel(IConfiguration configuration)
    {
        // Check for explicit HyperDX log level configuration
        var configuredLevel = configuration["Logging:LogLevel:HyperDX"];
        if (!string.IsNullOrWhiteSpace(configuredLevel) &&
            Enum.TryParse<LogEventLevel>(configuredLevel, true, out var level))
        {
            return level;
        }

        // Default: log everything to HyperDX (centralized aggregation)
        return LogEventLevel.Debug;
    }
}
