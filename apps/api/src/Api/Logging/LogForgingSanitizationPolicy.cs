using Serilog.Core;
using Serilog.Events;
using System.Collections;
using System.Reflection;

namespace Api.Logging;

/// <summary>
/// SEC-731: Serilog destructuring policy that sanitizes log values to prevent log forging attacks.
/// Removes carriage return (\r) and line feed (\n) characters from all string properties to prevent
/// attackers from injecting fake log entries by inserting newlines in user-provided input.
///
/// This policy applies to:
/// - Scalar string values
/// - String properties in complex objects
/// - Dictionary keys and values
/// - Nested objects (recursive)
///
/// Example attack prevented:
/// User input: "test\nINFO: Admin deleted user"
/// Without sanitization: Creates fake log entry
/// With sanitization: "testINFO: Admin deleted user" (newline removed)
/// </summary>
public class LogForgingSanitizationPolicy : IDestructuringPolicy
{
    /// <summary>
    /// Sanitizes a string by removing carriage return and line feed characters.
    /// </summary>
    public static string SanitizeString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // Remove both \r and \n characters to prevent log forging
        // Also handle URL-encoded variants that might have been decoded
        return value
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\u000D", string.Empty) // Unicode CR
            .Replace("\u000A", string.Empty); // Unicode LF
    }

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        if (value == null)
        {
            result = null;
            return false;
        }

        var type = value.GetType();

        // Handle strings - sanitize control characters
        if (value is string stringValue)
        {
            var sanitized = SanitizeString(stringValue);
            if (sanitized != stringValue)
            {
                result = new ScalarValue(sanitized);
                return true;
            }
            result = null;
            return false;
        }

        // Handle dictionaries BEFORE checking System namespace
        if (value is IDictionary dictionary)
        {
            var properties = new List<LogEventProperty>();
            foreach (DictionaryEntry entry in dictionary)
            {
                // SEC-731: Sanitize dictionary keys to prevent newline injection via keys
                var key = SanitizeString(entry.Key?.ToString());
                var propertyValue = entry.Value;

                if (propertyValue is string str)
                {
                    var sanitized = SanitizeString(str);
                    properties.Add(new LogEventProperty(key, new ScalarValue(sanitized)));
                }
                else if (propertyValue != null)
                {
                    // Try to recursively destructure nested objects with this policy
                    if (TryDestructure(propertyValue, propertyValueFactory, out var nestedResult))
                    {
                        properties.Add(new LogEventProperty(key, nestedResult!));
                    }
                    else
                    {
                        properties.Add(new LogEventProperty(key, propertyValueFactory.CreatePropertyValue(propertyValue, true)));
                    }
                }
            }
            result = new StructureValue(properties);
            return true;
        }

        // Skip primitive types and simple system types
        if (type.IsPrimitive || type.IsEnum ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(Guid) || type == typeof(TimeSpan) ||
            type == typeof(Uri))
        {
            result = null;
            return false;
        }

        // Handle complex objects via reflection
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (props.Length == 0)
        {
            result = null;
            return false;
        }

        var logProperties = new List<LogEventProperty>();
        foreach (var prop in props)
        {
            try
            {
                var propValue = prop.GetValue(value);

                if (propValue is string str)
                {
                    var sanitized = SanitizeString(str);
                    logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(sanitized)));
                }
                else if (propValue != null)
                {
                    // Try to recursively destructure nested objects with this policy
                    if (TryDestructure(propValue, propertyValueFactory, out var nestedResult))
                    {
                        logProperties.Add(new LogEventProperty(prop.Name, nestedResult!));
                    }
                    else
                    {
                        logProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(propValue, true)));
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Resilience pattern - logging infrastructure must not fail operations
            // Property access during logging may throw various exceptions; we must skip problematic properties
            catch (Exception ex) when (
                ex is TargetException or
                TargetInvocationException or
                TargetParameterCountException or
                MethodAccessException or
                NotSupportedException)
            {
                // Intentionally skip properties that throw on access during logging
                // This prevents logging infrastructure failures from propagating
            }
#pragma warning restore CA1031
        }

        if (logProperties.Count > 0)
        {
            result = new StructureValue(logProperties, type.Name);
            return true;
        }

        result = null;
        return false;
    }
}

/// <summary>
/// SEC-731: Serilog enricher that sanitizes all scalar string properties in log events.
/// This enricher processes log event properties after they've been created but before
/// they're written to sinks, ensuring all string values are sanitized for log forging.
///
/// Works in combination with LogForgingSanitizationPolicy to provide comprehensive protection:
/// - Policy: Handles complex objects during destructuring
/// - Enricher: Handles scalar string parameters passed directly to log methods
///
/// Example:
/// _logger.LogInformation("Query: {Query}", userInput);
/// Enricher sanitizes the {Query} property value before writing to logs.
/// </summary>
public class LogForgingSanitizationEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Process all properties in the log event
        var propertiesToUpdate = new List<(string key, LogEventPropertyValue value)>();

        foreach (var property in logEvent.Properties)
        {
            var sanitizedValue = SanitizeProperty(property.Value);
            if (sanitizedValue != null && sanitizedValue != property.Value)
            {
                propertiesToUpdate.Add((property.Key, sanitizedValue));
            }
        }

        // Apply sanitized values
        foreach (var (key, value) in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(key, value));
        }
    }

    /// <summary>
    /// Recursively sanitizes a log event property value.
    /// </summary>
    private LogEventPropertyValue? SanitizeProperty(LogEventPropertyValue value)
    {
        return value switch
        {
            // Sanitize scalar strings
            ScalarValue scalar when scalar.Value is string str =>
                new ScalarValue(LogForgingSanitizationPolicy.SanitizeString(str)),

            // Recursively sanitize sequence items
            SequenceValue sequence => new SequenceValue(
                sequence.Elements.Select(e => SanitizeProperty(e) ?? e)),

            // Recursively sanitize structure properties
            StructureValue structure => new StructureValue(
                structure.Properties.Select(p =>
                {
                    var sanitizedValue = SanitizeProperty(p.Value);
                    return sanitizedValue != null
                        ? new LogEventProperty(p.Name, sanitizedValue)
                        : p;
                }),
                structure.TypeTag),

            // Recursively sanitize dictionary items
            DictionaryValue dictionary => new DictionaryValue(
                dictionary.Elements.Select(kvp =>
                {
                    var sanitizedKey = SanitizeProperty(kvp.Key);
                    var sanitizedValue = SanitizeProperty(kvp.Value);
                    return new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        (sanitizedKey as ScalarValue) ?? kvp.Key,
                        sanitizedValue ?? kvp.Value);
                })),

            // No change needed for other types
            _ => null
        };
    }
}
