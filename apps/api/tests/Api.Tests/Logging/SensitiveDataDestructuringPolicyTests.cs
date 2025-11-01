using Api.Logging;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Logging;

/// <summary>
/// OPS-04: Unit tests for sensitive data redaction in structured logging.
/// Ensures passwords, API keys, tokens, and other secrets are properly redacted from logs.
/// </summary>
public class SensitiveDataDestructuringPolicyTests
{
    private readonly ITestOutputHelper _output;

    private readonly SensitiveDataDestructuringPolicy _policy;
    private readonly LogEventPropertyValueFactory _propertyFactory;

    public SensitiveDataDestructuringPolicyTests(ITestOutputHelper output)
    {
        _output = output;
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var passwordProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Password");
        passwordProp.Should().NotBeNull();
        var scalarValue = passwordProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("[REDACTED]");

        var usernameProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Username");
        usernameProp.Should().NotBeNull();
        var usernameValue = usernameProp.Value.Should().BeOfType<ScalarValue>().Subject;
        usernameValue.Value.Should().Be("admin");
    }

    [Fact]
    public void TryDestructure_WithApiKeyProperty_RedactsValue()
    {
        // Arrange
        var obj = new { ApiKey = "mpl_live_1234567890abcdefghijklmnopqrstuvwxyzABCD", Name = "Test" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var apiKeyProp = structureValue.Properties.FirstOrDefault(p => p.Name == "ApiKey");
        apiKeyProp.Should().NotBeNull();
        var scalarValue = apiKeyProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("[REDACTED]");
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var sensitiveProp = structureValue.Properties.FirstOrDefault(p => p.Name == propertyName);
        sensitiveProp.Should().NotBeNull();
        var scalarValue = sensitiveProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("[REDACTED]");

        var safeProp = structureValue.Properties.FirstOrDefault(p => p.Name == "SafeProperty");
        safeProp.Should().NotBeNull();
        var safeValue = safeProp.Value.Should().BeOfType<ScalarValue>().Subject;
        safeValue.Value.Should().Be("safe-value");
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var messageProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Message");
        messageProp.Should().NotBeNull();
        var scalarValue = messageProp.Value.Should().BeOfType<ScalarValue>().Subject;
        var message = scalarValue.Value?.ToString();
        message.Should().NotBeNull();
        message.Should().Contain("[REDACTED]");
        message.Should().NotContain(apiKey);
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var headerProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Header");
        headerProp.Should().NotBeNull();
        var scalarValue = headerProp.Value.Should().BeOfType<ScalarValue>().Subject;
        var header = scalarValue.Value?.ToString();
        header.Should().NotBeNull();
        header.Should().Contain("Bearer [REDACTED]");
    }

    [Fact]
    public void TryDestructure_WithConnectionStringContainingPassword_RedactsPassword()
    {
        // Arrange
        var obj = new { ConnectionString = "Host=localhost;Database=test;Username=admin;Password=secret123" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var connProp = structureValue.Properties.FirstOrDefault(p => p.Name == "ConnectionString");
        connProp.Should().NotBeNull();
        var scalarValue = connProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("[REDACTED]"); // Property name contains "password"
    }

    [Fact]
    public void TryDestructure_WithOpenAIStyleKey_RedactsKey()
    {
        // Arrange
        var obj = new { Message = "API Key: sk-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGH" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var messageProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Message");
        messageProp.Should().NotBeNull();
        var scalarValue = messageProp.Value.Should().BeOfType<ScalarValue>().Subject;
        var message = scalarValue.Value?.ToString();
        message.Should().NotBeNull();
        message.Should().Contain("[REDACTED]");
        message.Should().NotContain("sk-1234567890");
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        // Username should be visible
        var usernameProp = structureValue.Properties.FirstOrDefault(p => p.Name == "username");
        usernameProp.Should().NotBeNull();
        var usernameValue = usernameProp.Value.Should().BeOfType<ScalarValue>().Subject;
        usernameValue.Value.Should().Be("admin");

        // Password should be redacted
        var passwordProp = structureValue.Properties.FirstOrDefault(p => p.Name == "password");
        passwordProp.Should().NotBeNull();
        var passwordValue = passwordProp.Value.Should().BeOfType<ScalarValue>().Subject;
        passwordValue.Value.Should().Be("[REDACTED]");

        // API key should be redacted
        var apiKeyProp = structureValue.Properties.FirstOrDefault(p => p.Name == "api_key");
        apiKeyProp.Should().NotBeNull();
        var apiKeyValue = apiKeyProp.Value.Should().BeOfType<ScalarValue>().Subject;
        apiKeyValue.Value.Should().Be("[REDACTED]");

        // Description should be visible
        var descProp = structureValue.Properties.FirstOrDefault(p => p.Name == "description");
        descProp.Should().NotBeNull();
        var descValue = descProp.Value.Should().BeOfType<ScalarValue>().Subject;
        descValue.Value.Should().Be("Test user");
    }

    [Fact]
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        // Check nested credentials
        var credentialsProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Credentials");
        credentialsProp.Should().NotBeNull();
        var credentialsValue = credentialsProp.Value.Should().BeOfType<StructureValue>().Subject;

        var passwordProp = credentialsValue.Properties.FirstOrDefault(p => p.Name == "Password");
        passwordProp.Should().NotBeNull();
        var passwordValue = passwordProp.Value.Should().BeOfType<ScalarValue>().Subject;
        passwordValue.Value.Should().Be("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_WithNullValue_ReturnsFalse()
    {
        // Act
        var success = _policy.TryDestructure(null!, _propertyFactory, out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
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
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_WithSafeString_DoesNotRedact()
    {
        // Arrange
        var message = "This is a safe message with no sensitive data";

        // Act
        var success = _policy.TryDestructure(message, _propertyFactory, out var result);

        // Assert - should return false for simple string (no redaction needed)
        success.Should().BeFalse();
        result.Should().BeNull();
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
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var prop = structureValue.Properties.FirstOrDefault(p => p.Name == upperProp);
        prop.Should().NotBeNull();
        var scalarValue = prop.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("[REDACTED]");
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
