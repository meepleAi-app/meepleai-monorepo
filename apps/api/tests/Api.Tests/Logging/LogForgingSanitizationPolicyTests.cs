using Api.Logging;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Logging;

/// <summary>
/// SEC-731: Unit tests for log forging sanitization in structured logging.
/// Ensures carriage returns (\r) and line feeds (\n) are removed from all logged values
/// to prevent attackers from injecting fake log entries via newlines in user input.
///
/// Log forging example:
/// User input: "test\nINFO: Admin deleted user"
/// Without sanitization: Creates two log entries (one fake)
/// With sanitization: "testINFO: Admin deleted user" (single entry, newline removed)
/// </summary>
public class LogForgingSanitizationPolicyTests
{
    private readonly ITestOutputHelper _output;
    private readonly LogForgingSanitizationPolicy _policy;
    private readonly LogEventPropertyValueFactory _propertyFactory;

    public LogForgingSanitizationPolicyTests(ITestOutputHelper output)
    {
        _output = output;
        _policy = new LogForgingSanitizationPolicy();
        _propertyFactory = new LogEventPropertyValueFactory();
    }

    [Theory]
    [InlineData("test\nINFO: Fake log entry", "testINFO: Fake log entry")]
    [InlineData("test\rINFO: Fake log entry", "testINFO: Fake log entry")]
    [InlineData("test\r\nINFO: Fake log entry", "testINFO: Fake log entry")]
    [InlineData("Normal query without newlines", "Normal query without newlines")]
    [InlineData("", "")]
    public void SanitizeString_RemovesCarriageReturnAndLineFeed(string input, string expected)
    {
        // Act
        var result = LogForgingSanitizationPolicy.SanitizeString(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeString_WithNullInput_ReturnsEmptyString()
    {
        // Act
        var result = LogForgingSanitizationPolicy.SanitizeString(null);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void TryDestructure_WithStringContainingNewline_SanitizesValue()
    {
        // Arrange
        var obj = new { Query = "search\nINFO: Admin deleted user" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var queryProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Query");
        queryProp.Should().NotBeNull();
        var scalarValue = queryProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("searchINFO: Admin deleted user");
    }

    [Fact]
    public void TryDestructure_WithStringContainingCarriageReturn_SanitizesValue()
    {
        // Arrange
        var obj = new { Query = "search\rERROR: Fake error" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var queryProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Query");
        queryProp.Should().NotBeNull();
        var scalarValue = queryProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("searchERROR: Fake error");
    }

    [Fact]
    public void TryDestructure_WithStringContainingBothCrAndLf_SanitizesBoth()
    {
        // Arrange
        var obj = new { Email = "test@example.com\r\nWARNING: Injected log" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var emailProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Email");
        emailProp.Should().NotBeNull();
        var scalarValue = emailProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("test@example.comWARNING: Injected log");
    }

    [Fact]
    public void TryDestructure_WithUnicodeControlCharacters_SanitizesValue()
    {
        // Arrange - Unicode representations of CR and LF
        var obj = new { Query = "search\u000D\u000AINFO: Unicode injection" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var queryProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Query");
        queryProp.Should().NotBeNull();
        var scalarValue = queryProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("searchINFO: Unicode injection");
    }

    [Fact]
    public void TryDestructure_WithMultipleProperties_SanitizesAllStrings()
    {
        // Arrange
        var obj = new
        {
            Query = "test\nfake",
            Email = "user@test.com\rfake",
            Name = "Normal Name",
            Count = 42
        };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var queryProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Query");
        queryProp.Should().NotBeNull();
        var queryValue = queryProp.Value.Should().BeOfType<ScalarValue>().Subject;
        queryValue.Value.Should().Be("testfake");

        var emailProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Email");
        emailProp.Should().NotBeNull();
        var emailValue = emailProp.Value.Should().BeOfType<ScalarValue>().Subject;
        emailValue.Value.Should().Be("user@test.comfake");

        var nameProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Name");
        nameProp.Should().NotBeNull();
        var nameValue = nameProp.Value.Should().BeOfType<ScalarValue>().Subject;
        nameValue.Value.Should().Be("Normal Name");
    }

    [Fact]
    public void TryDestructure_WithDictionary_SanitizesStringValues()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "query", "search\nINFO: Fake" },
            { "email", "test@example.com\rfake" },
            { "safe", "Normal value" }
        };

        // Act
        var success = _policy.TryDestructure(dict, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var queryProp = structureValue.Properties.FirstOrDefault(p => p.Name == "query");
        queryProp.Should().NotBeNull();
        var queryValue = queryProp.Value.Should().BeOfType<ScalarValue>().Subject;
        queryValue.Value.Should().Be("searchINFO: Fake");

        var emailProp = structureValue.Properties.FirstOrDefault(p => p.Name == "email");
        emailProp.Should().NotBeNull();
        var emailValue = emailProp.Value.Should().BeOfType<ScalarValue>().Subject;
        emailValue.Value.Should().Be("test@example.comfake");

        var safeProp = structureValue.Properties.FirstOrDefault(p => p.Name == "safe");
        safeProp.Should().NotBeNull();
        var safeValue = safeProp.Value.Should().BeOfType<ScalarValue>().Subject;
        safeValue.Value.Should().Be("Normal value");
    }

    [Fact]
    public void TryDestructure_WithDictionaryMaliciousKeys_SanitizesKeys()
    {
        // Arrange - SEC-731: Attacker controls dictionary keys (e.g., HTTP headers)
        var dict = new Dictionary<string, string>
        {
            { "X-Header\nINFO: Fake log entry", "value1" },
            { "User-Agent\rERROR: Injected", "value2" },
            { "Safe-Header", "value3" }
        };

        // Act
        var success = _policy.TryDestructure(dict, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        // Verify keys are sanitized (newlines removed)
        var sanitizedKeyProp1 = structureValue.Properties.FirstOrDefault(p => p.Name == "X-HeaderINFO: Fake log entry");
        sanitizedKeyProp1.Should().NotBeNull("key should be sanitized");

        var sanitizedKeyProp2 = structureValue.Properties.FirstOrDefault(p => p.Name == "User-AgentERROR: Injected");
        sanitizedKeyProp2.Should().NotBeNull("key should be sanitized");

        var safeProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Safe-Header");
        safeProp.Should().NotBeNull();

        // Verify no keys contain newlines
        foreach (var prop in structureValue.Properties)
        {
            prop.Name.Should().NotContain("\n");
            prop.Name.Should().NotContain("\r");
        }
    }

    [Fact]
    public void TryDestructure_WithNestedObject_SanitizesAtAllLevels()
    {
        // Arrange
        var obj = new
        {
            TopLevel = "top\nfake",
            Nested = new
            {
                Inner = "inner\rfake",
                Safe = "Normal"
            }
        };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        // Check top-level property
        var topProp = structureValue.Properties.FirstOrDefault(p => p.Name == "TopLevel");
        topProp.Should().NotBeNull();
        var topValue = topProp.Value.Should().BeOfType<ScalarValue>().Subject;
        topValue.Value.Should().Be("topfake");

        // Check nested object
        var nestedProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Nested");
        nestedProp.Should().NotBeNull();
        var nestedValue = nestedProp.Value.Should().BeOfType<StructureValue>().Subject;

        var innerProp = nestedValue.Properties.FirstOrDefault(p => p.Name == "Inner");
        innerProp.Should().NotBeNull();
        var innerValue = innerProp.Value.Should().BeOfType<ScalarValue>().Subject;
        innerValue.Value.Should().Be("innerfake");
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
    public void TryDestructure_WithSafeString_DoesNotModify()
    {
        // Arrange
        var message = "This is a safe message with no control characters";

        // Act
        var success = _policy.TryDestructure(message, _propertyFactory, out var result);

        // Assert - should return false for simple string (no modification needed)
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("user input\nINFO: [2025-01-01 12:00:00] Admin deleted all users")]
    [InlineData("query\nERROR: System failure\nWARNING: Security breach")]
    [InlineData("test\r\nINFO: Multiple\r\nfake\r\nlog\r\nentries")]
    public void TryDestructure_WithComplexLogForgingAttempts_SanitizesAll(string maliciousInput)
    {
        // Arrange
        var obj = new { UserInput = maliciousInput };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var prop = structureValue.Properties.FirstOrDefault(p => p.Name == "UserInput");
        prop.Should().NotBeNull();
        var scalarValue = prop.Value.Should().BeOfType<ScalarValue>().Subject;
        var sanitized = scalarValue.Value?.ToString();
        sanitized.Should().NotContain("\n");
        sanitized.Should().NotContain("\r");
    }

    [Fact]
    public void TryDestructure_WithEmptyString_ReturnsOriginalValue()
    {
        // Arrange
        var obj = new { Query = "" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue(); // Policy processes empty strings (sanitizes them)
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryDestructure_WithOnlyNewlines_RemovesAll()
    {
        // Arrange
        var obj = new { Query = "\n\r\n\r" };

        // Act
        var success = _policy.TryDestructure(obj, _propertyFactory, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        var structureValue = result.Should().BeOfType<StructureValue>().Subject;

        var queryProp = structureValue.Properties.FirstOrDefault(p => p.Name == "Query");
        queryProp.Should().NotBeNull();
        var scalarValue = queryProp.Value.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be(string.Empty);
    }
}

/// <summary>
/// SEC-731: Tests for LogForgingSanitizationEnricher that processes log events.
/// </summary>
public class LogForgingSanitizationEnricherTests
{
    private readonly LogForgingSanitizationEnricher _enricher;
    private readonly ITestOutputHelper _output;

    public LogForgingSanitizationEnricherTests(ITestOutputHelper output)
    {
        _output = output;
        _enricher = new LogForgingSanitizationEnricher();
    }

    [Fact]
    public void Enrich_WithScalarStringProperty_SanitizesNewlines()
    {
        // Arrange
        var logEvent = CreateLogEvent("Query: {Query}", "search\nINFO: Fake");

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        var queryProp = logEvent.Properties["Query"];
        var scalarValue = queryProp.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("searchINFO: Fake");
    }

    [Fact]
    public void Enrich_WithMultipleProperties_SanitizesAll()
    {
        // Arrange
        var template = "Query: {Query}, Email: {Email}";
        var properties = new List<LogEventProperty>
        {
            new("Query", new ScalarValue("test\nfake")),
            new("Email", new ScalarValue("user@test.com\rfake"))
        };
        var logEvent = CreateLogEventWithProperties(template, properties);

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        var queryValue = logEvent.Properties["Query"].Should().BeOfType<ScalarValue>().Subject;
        queryValue.Value.Should().Be("testfake");

        var emailValue = logEvent.Properties["Email"].Should().BeOfType<ScalarValue>().Subject;
        emailValue.Value.Should().Be("user@test.comfake");
    }

    [Fact]
    public void Enrich_WithSafeString_DoesNotModify()
    {
        // Arrange
        var logEvent = CreateLogEvent("Query: {Query}", "safe query");

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        var queryProp = logEvent.Properties["Query"];
        var scalarValue = queryProp.Should().BeOfType<ScalarValue>().Subject;
        scalarValue.Value.Should().Be("safe query");
    }

    [Fact]
    public void Enrich_WithStructureValue_SanitizesNestedStrings()
    {
        // Arrange
        var structureProperties = new List<LogEventProperty>
        {
            new("Name", new ScalarValue("test\nfake")),
            new("Count", new ScalarValue(42))
        };
        var structureValue = new StructureValue(structureProperties, "TestObject");

        var properties = new List<LogEventProperty>
        {
            new("Data", structureValue)
        };
        var logEvent = CreateLogEventWithProperties("Data: {@Data}", properties);

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        var dataProp = logEvent.Properties["Data"];
        var structure = dataProp.Should().BeOfType<StructureValue>().Subject;
        var nameProp = structure.Properties.First(p => p.Name == "Name");
        var nameValue = nameProp.Value.Should().BeOfType<ScalarValue>().Subject;
        nameValue.Value.Should().Be("testfake");
    }

    // Helper methods
    private LogEvent CreateLogEvent(string template, string propertyValue)
    {
        var properties = new List<LogEventProperty>
        {
            new("Query", new ScalarValue(propertyValue))
        };
        return CreateLogEventWithProperties(template, properties);
    }

    private LogEvent CreateLogEventWithProperties(string template, List<LogEventProperty> properties)
    {
        var messageTemplate = new Serilog.Parsing.MessageTemplateParser().Parse(template);
        var propertyDict = properties.ToDictionary(p => p.Name, p => p.Value);

        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            messageTemplate,
            propertyDict.Select(kvp => new LogEventProperty(kvp.Key, kvp.Value)));
    }
}
