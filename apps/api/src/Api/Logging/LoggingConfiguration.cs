using Serilog;
using Serilog.Events;

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

        // Add Seq sink when configured (monitoring profile or SEQ_URL env var set)
        var seqUrl = configuration["Seq:ServerUrl"]
                  ?? Environment.GetEnvironmentVariable("SEQ_URL");
        if (!string.IsNullOrEmpty(seqUrl))
        {
            loggerConfig.WriteTo.Seq(
                serverUrl: seqUrl,
                apiKey: configuration["Seq:ApiKey"],
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
        }

        return loggerConfig;
    }

    /// <summary>
    /// Gets the default log level based on environment.
    /// </summary>
    private static LogEventLevel GetDefaultLogLevel(string environmentName, ConfigurationManager configuration)
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
    private static LogEventLevel GetLogLevel(ConfigurationManager configuration, string key, LogEventLevel defaultLevel)
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

}
