using Serilog;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// OPS-04: Centralized logging configuration for environment-based log levels and structured logging.
/// Provides consistent Serilog setup across all environments with appropriate defaults.
/// </summary>
public static class LoggingConfiguration
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

        // Seq configuration
        var seqUrl = configuration["SEQ_URL"] ?? "http://seq:5341";
        var seqApiKey = configuration["SEQ_API_KEY"];

        // Build logger configuration
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(defaultLogLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", aspNetCoreLogLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", efCoreLogLevel)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning) // Reduce HTTP client noise
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "MeepleAI")
            .Enrich.WithProperty("Environment", environment.EnvironmentName);

        // Add sensitive data redaction
        loggerConfig.Destructure.With<SensitiveDataDestructuringPolicy>();

        // Console sink with appropriate formatting
        var consoleTemplate = environment.IsDevelopment()
            ? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            : "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

        loggerConfig.WriteTo.Console(
            outputTemplate: consoleTemplate,
            restrictedToMinimumLevel: GetConsoleLogLevel(environment.EnvironmentName));

        // Add Seq sink if configured
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            loggerConfig.WriteTo.Seq(
                serverUrl: seqUrl,
                apiKey: seqApiKey,
                restrictedToMinimumLevel: GetSeqLogLevel(environment.EnvironmentName, configuration));
        }

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
    /// Gets the Seq log level based on environment and configuration.
    /// All environments should log to Seq, but threshold may vary.
    /// </summary>
    private static LogEventLevel GetSeqLogLevel(string environmentName, IConfiguration configuration)
    {
        // Check for explicit Seq log level configuration
        var configuredLevel = configuration["Logging:LogLevel:Seq"];
        if (!string.IsNullOrWhiteSpace(configuredLevel) &&
            Enum.TryParse<LogEventLevel>(configuredLevel, true, out var level))
        {
            return level;
        }

        // Default: log everything to Seq (centralized aggregation)
        return LogEventLevel.Debug;
    }
}
