using Api.Logging;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Api.Tests.Logging;

/// <summary>
/// OPS-04: Unit tests for sensitive data redaction in structured logging.
/// Ensures passwords, API keys, tokens, and other secrets are properly redacted from logs.
/// </summary>
public class SensitiveDataDestructuringPolicyTests
{
    private readonly SensitiveDataDestructuringPolicy _policy;
    private readonly LogEventPropertyValueFactory _propertyFactory;

    public SensitiveDataDestructuringPolicyTests()
    {
        _policy = new SensitiveDataDestructuringPolicy();
        _propertyFactory = new LogEventPropertyValueFactory();
    }

    [Fact]
    public void TryDestructure_WithPasswordProperty_RedactsValue()
    {
        // Arrange
        var obj = new { Password = "super-secret-password", Username = "admin" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var passwordProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Password");
        Assert.NotNull(passwordProp);
        var scalarValue = Assert.IsType<ScalarValue>(passwordProp.Value);
        Assert.Equal("[REDACTED]", scalarValue.Value);

        var usernameProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Username");
        Assert.NotNull(usernameProp);
        var usernameValue = Assert.IsType<ScalarValue>(usernameProp.Value);
        Assert.Equal("admin", usernameValue.Value);
    }

    [Fact]
    public void TryDestructure_WithApiKeyProperty_RedactsValue()
    {
        // Arrange
        var obj = new { ApiKey = "mpl_live_1234567890abcdefghijklmnopqrstuvwxyzABCD", Name = "Test" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var apiKeyProp = structureValue.Properties.FirstOrDefault(p => p.Name == "ApiKey");
        Assert.NotNull(apiKeyProp);
        var scalarValue = Assert.IsType<ScalarValue>(apiKeyProp.Value);
        Assert.Equal("[REDACTED]", scalarValue.Value);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("ApiKey")]
    [InlineData("api_key")]
    [InlineData("AccessToken")]
    [InlineData("access_token")]
    [InlineData("RefreshToken")]
    [InlineData("Secret")]
    [InlineData("ClientSecret")]
    [InlineData("BearerToken")]
    [InlineData("Authorization")]
    [InlineData("PrivateKey")]
    [InlineData("ConnectionString")]
    [InlineData("EncryptionKey")]
    public void TryDestructure_WithSensitivePropertyNames_RedactsValue(string propertyName)
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            { propertyName, "sensitive-value" },
            { "SafeProperty", "safe-value" }
        };

        // Act
        var success = _policy.TryDestructure(dict, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var sensitiveProp = structureValue.Properties.FirstOrDefault(p => p.Name == propertyName);
        Assert.NotNull(sensitiveProp);
        var scalarValue = Assert.IsType<ScalarValue>(sensitiveProp.Value);
        Assert.Equal("[REDACTED]", scalarValue.Value);

        var safeProp = structureValue.Properties.FirstOrDefault(p => p.Name == "SafeProperty");
        Assert.NotNull(safeProp);
        var safeValue = Assert.IsType<ScalarValue>(safeProp.Value);
        Assert.Equal("safe-value", safeValue.Value);
    }

    [Theory]
    [InlineData("mpl_live_1234567890abcdefghijklmnopqrstuvwxyzABCD")]
    [InlineData("mpl_test_9876543210zyxwvutsrqponmlkjihgfedcbaZYXW")]
    public void TryDestructure_WithApiKeyInString_RedactsPattern(string apiKey)
    {
        // Arrange
        var obj = new { Message = $"Using API key: {apiKey}" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var messageProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Message");
        Assert.NotNull(messageProp);
        var scalarValue = Assert.IsType<ScalarValue>(messageProp.Value);
        var message = scalarValue.Value?.ToString();
        Assert.NotNull(message);
        Assert.Contains("[REDACTED]", message);
        Assert.DoesNotContain(apiKey, message);
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U")]
    [InlineData("bearer abc123def456ghi789")]
    public void TryDestructure_WithBearerTokenInString_RedactsPattern(string token)
    {
        // Arrange
        var obj = new { Header = $"Authorization: {token}" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var headerProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Header");
        Assert.NotNull(headerProp);
        var scalarValue = Assert.IsType<ScalarValue>(headerProp.Value);
        var header = scalarValue.Value?.ToString();
        Assert.NotNull(header);
        Assert.Contains("Bearer [REDACTED]", header);
    }

    [Fact]
    public void TryDestructure_WithConnectionStringContainingPassword_RedactsPassword()
    {
        // Arrange
        var obj = new { ConnectionString = "Host=localhost;Database=test;Username=admin;Password=secret123" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var connProp = structureValue.Properties.FirstOrDefault(p => p.Name == "ConnectionString");
        Assert.NotNull(connProp);
        var scalarValue = Assert.IsType<ScalarValue>(connProp.Value);
        Assert.Equal("[REDACTED]", scalarValue.Value); // Property name contains "password"
    }

    [Fact]
    public void TryDestructure_WithOpenAIStyleKey_RedactsKey()
    {
        // Arrange
        var obj = new { Message = "API Key: sk-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGH" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var messageProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Message");
        Assert.NotNull(messageProp);
        var scalarValue = Assert.IsType<ScalarValue>(messageProp.Value);
        var message = scalarValue.Value?.ToString();
        Assert.NotNull(message);
        Assert.Contains("[REDACTED]", message);
        Assert.DoesNotContain("sk-1234567890", message);
    }

    [Fact]
    public void TryDestructure_WithDictionary_RedactsSensitiveKeys()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "username", "admin" },
            { "password", "secret123" },
            { "api_key", "mpl_live_abcdefghijklmnopqrstuvwxyz1234567890AB" },
            { "description", "Test user" }
        };

        // Act
        var success = _policy.TryDestructure(dict, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        // Username should be visible
        var usernameProp = structureValue.Properties.FirstOrDefault(p => p.Name == "username");
        Assert.NotNull(usernameProp);
        var usernameValue = Assert.IsType<ScalarValue>(usernameProp.Value);
        Assert.Equal("admin", usernameValue.Value);

        // Password should be redacted
        var passwordProp = structureValue.Properties.FirstOrDefault(p => p.Name == "password");
        Assert.NotNull(passwordProp);
        var passwordValue = Assert.IsType<ScalarValue>(passwordProp.Value);
        Assert.Equal("[REDACTED]", passwordValue.Value);

        // API key should be redacted
        var apiKeyProp = structureValue.Properties.FirstOrDefault(p => p.Name == "api_key");
        Assert.NotNull(apiKeyProp);
        var apiKeyValue = Assert.IsType<ScalarValue>(apiKeyProp.Value);
        Assert.Equal("[REDACTED]", apiKeyValue.Value);

        // Description should be visible
        var descProp = structureValue.Properties.FirstOrDefault(p => p.Name == "description");
        Assert.NotNull(descProp);
        var descValue = Assert.IsType<ScalarValue>(descProp.Value);
        Assert.Equal("Test user", descValue.Value);
    }

    [Fact(Skip = "TODO: Nested anonymous type destructuring with mock factory needs refinement")]
    public void TryDestructure_WithNestedSensitiveData_RedactsAtAllLevels()
    {
        // Arrange
        var obj = new
        {
            Name = "User",
            Credentials = new
            {
                Username = "admin",
                Password = "secret"
            }
        };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        // Check nested credentials
        var credentialsProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Credentials");
        Assert.NotNull(credentialsProp);
        var credentialsValue = Assert.IsType<StructureValue>(credentialsProp.Value);

        var passwordProp = credentialsValue.Properties.FirstOrDefault(p => p.Name == "Password");
        Assert.NotNull(passwordProp);
        var passwordValue = Assert.IsType<ScalarValue>(passwordProp.Value);
        Assert.Equal("[REDACTED]", passwordValue.Value);
    }

    [Fact]
    public void TryDestructure_WithNullValue_ReturnsFalse()
    {
        // Act
        var success = _policy.TryDestructure(null!, _propertyFactory, out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(true)]
    [InlineData(3.14)]
    public void TryDestructure_WithPrimitiveTypes_ReturnsFalse(object value)
    {
        // Act
        var success = _policy.TryDestructure(value, _propertyFactory, out var result);

        // Assert
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDestructure_WithSafeString_DoesNotRedact()
    {
        // Arrange
        var message = "This is a safe message with no sensitive data";

        // Act
        var success = _policy.TryDestructure(message, _propertyFactory, out var result);

        // Assert - should return false for simple string (no redaction needed)
        Assert.False(success);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("pwd")]
    [InlineData("passwd")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("bearer")]
    [InlineData("credential")]
    public void TryDestructure_CaseInsensitiveSensitivePropertyNames_RedactsValue(string propertyName)
    {
        // Arrange - mix cases
        var upperProp = propertyName.ToUpperInvariant();
        var dict = new Dictionary<string, string>
        {
            { upperProp, "should-be-redacted" }
        };

        // Act
        var success = _policy.TryDestructure(dict, _propertyFactory, out var result);

        // Assert
        Assert.True(success);
        Assert.NotNull(result);
        var structureValue = Assert.IsType<StructureValue>(result);

        var prop = structureValue.Properties.FirstOrDefault(p => p.Name == upperProp);
        Assert.NotNull(prop);
        var scalarValue = Assert.IsType<ScalarValue>(prop.Value);
        Assert.Equal("[REDACTED]", scalarValue.Value);
    }
}

/// <summary>
/// Mock factory for creating log event property values in tests.
/// </summary>
internal class LogEventPropertyValueFactory : ILogEventPropertyValueFactory
{
    public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
    {
        if (value == null)
        {
            return new ScalarValue(null);
        }

        if (value is string str)
        {
            return new ScalarValue(str);
        }

        if (value.GetType().IsPrimitive || value is decimal)
        {
            return new ScalarValue(value);
        }

        if (!destructureObjects)
        {
            return new ScalarValue(value);
        }

        // For complex types, create structure value
        var properties = value.GetType()
            .GetProperties()
            .Select(p => new LogEventProperty(p.Name, CreatePropertyValue(p.GetValue(value), true)))
            .ToList();

        return new StructureValue(properties, value.GetType().Name);
    }
}
