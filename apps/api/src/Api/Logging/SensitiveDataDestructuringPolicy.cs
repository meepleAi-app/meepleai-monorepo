using Serilog.Core;
using Serilog.Events;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Api.Logging;

/// <summary>
/// OPS-04: Serilog destructuring policy that redacts sensitive data from log events.
/// Prevents passwords, API keys, tokens, and other secrets from appearing in logs.
/// </summary>
public partial class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly string[] SensitivePropertyNames = new[]
    {
        "password",
        "passwd",
        "pwd",
        "secret",
        "apikey",
        "api_key",
        "token",
        "accesstoken",
        "access_token",
        "refreshtoken",
        "refresh_token",
        "bearer",
        "authorization",
        "credential",
        "credentials",
        "connectionstring",
        "connection_string",
        "privatekey",
        "private_key",
        "securitykey",
        "security_key",
        "sessionid",
        "session_id",
        "cookie",
        "csrf",
        "xsrf",
        "salt",
        "hash",
        "key",
        "encryptionkey",
        "encryption_key",
        "clientsecret",
        "client_secret",
        "clientid",
        "client_id",
        "jwttoken",
        "jwt_token",
        "bearertoken",
        "bearer_token"
    };

    // Regex patterns for detecting sensitive data in strings
    [GeneratedRegex(@"mpl_(live|test)_[A-Za-z0-9+/]{40}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"sk-[A-Za-z0-9]{20,}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OpenAIKeyPattern();

    [GeneratedRegex(@"(password|passwd|pwd)\s*=\s*[^\s;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PasswordInConnectionStringPattern();

    private const string RedactedValue = "[REDACTED]";

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        if (value == null)
        {
            result = null;
            return false;
        }

        var type = value.GetType();

        // Handle strings - check for sensitive patterns
        if (value is string stringValue)
        {
            var redacted = RedactSensitiveStringPatterns(stringValue);
            if (redacted != stringValue)
            {
                result = new ScalarValue(redacted);
                return true;
            }
            result = null;
            return false;
        }

        // Handle dictionaries BEFORE checking System namespace
        // (Dictionary is in System.Collections.Generic)
        if (value is IDictionary dictionary)
        {
            var properties = new List<LogEventProperty>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? "null";
                var propertyValue = entry.Value;

                if (IsSensitiveProperty(key))
                {
                    properties.Add(new LogEventProperty(key, new ScalarValue(RedactedValue)));
                }
                else if (propertyValue is string str)
                {
                    var redacted = RedactSensitiveStringPatterns(str);
                    properties.Add(new LogEventProperty(key, new ScalarValue(redacted)));
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

        // Skip primitive types and simple system types (after handling collections)
        // Note: We don't skip ALL System types to allow anonymous types and other compiler-generated types
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
                if (IsSensitiveProperty(prop.Name))
                {
                    logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(RedactedValue)));
                }
                else if (propValue is string str)
                {
                    var redacted = RedactSensitiveStringPatterns(str);
                    logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(redacted)));
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
            catch
            {
                // Skip properties that throw on access
            }
        }

        if (logProperties.Count > 0)
        {
            result = new StructureValue(logProperties, type.Name);
            return true;
        }

        result = null;
        return false;
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var normalized = propertyName.ToLowerInvariant().Replace("_", "").Replace("-", "");
        return SensitivePropertyNames.Any(sensitive => normalized.Contains(sensitive));
    }

    private static string RedactSensitiveStringPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // Redact API keys (mpl_live_xxx or mpl_test_xxx)
        if (ApiKeyPattern().IsMatch(input))
        {
            input = ApiKeyPattern().Replace(input, RedactedValue);
        }

        // Redact Bearer tokens
        if (BearerTokenPattern().IsMatch(input))
        {
            input = BearerTokenPattern().Replace(input, "Bearer [REDACTED]");
        }

        // Redact OpenAI-style keys
        if (OpenAIKeyPattern().IsMatch(input))
        {
            input = OpenAIKeyPattern().Replace(input, RedactedValue);
        }

        // Redact passwords in connection strings
        if (PasswordInConnectionStringPattern().IsMatch(input))
        {
            input = PasswordInConnectionStringPattern().Replace(input, "password=[REDACTED]");
        }

        return input;
    }
}
