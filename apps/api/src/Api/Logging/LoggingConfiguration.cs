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
            restrictedToMinimumLevel: GetConsoleLogLevel(environment.EnvironmentName, configuration));

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
    /// Livello minimo del sink console: <c>Logging:Console:MinimumLevel</c> se impostato,
    /// altrimenti il default dell'ambiente (#3768).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prima era cablato e basta. Conseguenza pratica: una riga emessa a <c>Debug</c> viene
    /// generata e poi scartata prima di stdout, quindi <c>docker logs</c> non la mostra mai —
    /// nemmeno alzando <c>Logging:LogLevel:Default</c>, che agisce sul livello globale e non su
    /// questo filtro a valle. È costato due tentativi di raccolta del dump <c>[RAG-TUNE]</c> su
    /// staging prima di risalire alla causa.
    /// </para>
    /// <para>
    /// In produzione il sink sta a <c>Warning</c>: senza questo override nemmeno gli
    /// <c>Information</c> sono visibili, e una diagnosi su un incidente reale parte cieca.
    /// </para>
    /// <para>
    /// I default per ambiente restano invariati, quindi il comportamento non cambia finché la
    /// chiave non viene impostata. Un valore non parsabile ricade sul default invece di spegnere
    /// il logging o far fallire l'avvio.
    /// </para>
    /// </remarks>
    internal static LogEventLevel GetConsoleLogLevel(string environmentName, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration["Logging:Console:MinimumLevel"];
        if (!string.IsNullOrWhiteSpace(configured) &&
            Enum.TryParse<LogEventLevel>(configured, true, out var overridden))
        {
            return overridden;
        }

        return environmentName.ToLowerInvariant() switch
        {
            "development" => LogEventLevel.Debug,
            "staging" => LogEventLevel.Information,
            "production" => LogEventLevel.Warning, // Less verbose in prod
            _ => LogEventLevel.Information
        };
    }

}
