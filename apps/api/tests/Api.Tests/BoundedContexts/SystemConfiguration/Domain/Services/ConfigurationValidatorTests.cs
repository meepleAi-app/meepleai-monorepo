using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Services;

/// <summary>
/// Tests for the ConfigurationValidator domain service.
/// Issue #3025: Backend 90% Coverage Target - Phase 14
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorTests()
    {
        _validator = new ConfigurationValidator();
    }

    #region Input Validation Tests

    [Fact]
    public void Validate_WithEmptyKey_ThrowsArgumentException()
    {
        // Act
        var action = () => _validator.Validate("", "value", "string");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Key cannot be empty*");
    }

    [Fact]
    public void Validate_WithNullKey_ThrowsArgumentException()
    {
        // Act
        var action = () => _validator.Validate(null!, "value", "string");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Key cannot be empty*");
    }

    [Fact]
    public void Validate_WithWhitespaceKey_ThrowsArgumentException()
    {
        // Act
        var action = () => _validator.Validate("   ", "value", "string");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Key cannot be empty*");
    }

    [Fact]
    public void Validate_WithEmptyValue_ThrowsArgumentException()
    {
        // Act
        var action = () => _validator.Validate("key", "", "string");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be empty*");
    }

    [Fact]
    public void Validate_WithEmptyValueType_ThrowsArgumentException()
    {
        // Act
        var action = () => _validator.Validate("key", "value", "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ValueType cannot be empty*");
    }

    #endregion

    #region String Type Validation Tests

    [Fact]
    public void Validate_StringType_WithAnyValue_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "any value here", "string");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_StringType_WithNumbers_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "12345", "string");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Integer Type Validation Tests

    [Theory]
    [InlineData("123")]
    [InlineData("-456")]
    [InlineData("0")]
    [InlineData("2147483647")]
    public void Validate_IntegerType_WithValidInteger_ReturnsValid(string value)
    {
        // Act
        var result = _validator.Validate("TestKey", value, "int");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_IntegerType_WithAlias_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "123", "integer");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("12,345")]
    [InlineData("")]
    public void Validate_IntegerType_WithInvalidValue_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("TestKey", value.Length > 0 ? value : "abc", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not a valid integer"));
    }

    #endregion

    #region Long Type Validation Tests

    [Theory]
    [InlineData("123")]
    [InlineData("-9223372036854775808")]
    [InlineData("9223372036854775807")]
    public void Validate_LongType_WithValidLong_ReturnsValid(string value)
    {
        // Act
        var result = _validator.Validate("TestKey", value, "long");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_LongType_WithInvalidValue_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("TestKey", "not-a-number", "long");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not a valid long integer"));
    }

    #endregion

    #region Decimal/Float/Double Type Validation Tests

    [Theory]
    [InlineData("123.45")]
    [InlineData("-0.5")]
    [InlineData("0")]
    [InlineData("1E10")]
    public void Validate_DoubleType_WithValidDouble_ReturnsValid(string value)
    {
        // Act
        var result = _validator.Validate("TestKey", value, "double");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FloatType_WithValidValue_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "3.14", "float");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DecimalType_WithValidValue_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "99.99", "decimal");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DoubleType_WithInvalidValue_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("TestKey", "not-a-number", "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not a valid decimal number"));
    }

    #endregion

    #region Boolean Type Validation Tests

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    public void Validate_BooleanType_WithValidBoolean_ReturnsValid(string value)
    {
        // Act
        var result = _validator.Validate("TestKey", value, "bool");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BooleanType_WithAlias_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "true", "boolean");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("maybe")]
    public void Validate_BooleanType_WithInvalidValue_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("TestKey", value, "bool");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not a valid boolean"));
    }

    #endregion

    #region JSON Type Validation Tests

    [Fact]
    public void Validate_JsonType_WithValidJson_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "{\"name\":\"test\"}", "json");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_JsonType_WithValidJsonArray_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("TestKey", "[1,2,3]", "json");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_JsonType_WithComplexJson_ReturnsValid()
    {
        // Arrange
        var json = "{\"users\":[{\"id\":1,\"name\":\"test\"}],\"count\":1}";

        // Act
        var result = _validator.Validate("TestKey", json, "json");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_JsonType_WithInvalidJson_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("TestKey", "{invalid json}", "json");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not valid JSON"));
    }

    #endregion

    #region Unknown Type Validation Tests

    [Fact]
    public void Validate_UnknownType_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("TestKey", "value", "unknowntype");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Unknown value type"));
    }

    #endregion

    #region Rate Limit Rules Tests

    [Fact]
    public void Validate_RateLimitMaxTokens_WithNegativeValue_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("RateLimit:MaxTokens", "-1", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxTokens must be non-negative"));
    }

    [Fact]
    public void Validate_RateLimitMaxTokens_WithZero_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("RateLimit:MaxTokens", "0", "int");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RateLimitRefillRate_WithNegativeValue_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("RateLimit:RefillRate", "-10", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("RefillRate must be non-negative"));
    }

    [Fact]
    public void Validate_RateLimitWindowSeconds_WithZero_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("RateLimit:WindowSeconds", "0", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("WindowSeconds must be at least 1"));
    }

    [Fact]
    public void Validate_RateLimitWindowSeconds_WithPositive_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("RateLimit:WindowSeconds", "60", "int");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region AI/LLM Rules Tests

    [Fact]
    public void Validate_AiTemperature_WithValidRange_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("AI:Temperature", "0.7", "double");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("2.1")]
    [InlineData("3")]
    public void Validate_AiTemperature_WithInvalidRange_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("AI:Temperature", value, "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Temperature must be between 0 and 2"));
    }

    [Fact]
    public void Validate_LlmMaxTokens_WithZero_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("LLM:MaxTokens", "0", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxTokens must be positive"));
    }

    [Theory]
    [InlineData("-0.5")]
    [InlineData("1.5")]
    public void Validate_AiTopP_WithInvalidRange_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("AI:TopP", value, "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TopP must be between 0 and 1"));
    }

    [Theory]
    [InlineData("-2.5")]
    [InlineData("2.5")]
    public void Validate_AiFrequencyPenalty_WithInvalidRange_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("AI:FrequencyPenalty", value, "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Penalty values must be between -2 and 2"));
    }

    [Fact]
    public void Validate_AiPresencePenalty_WithInvalidRange_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("AI:PresencePenalty", "3", "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Penalty values must be between -2 and 2"));
    }

    #endregion

    #region RAG Rules Tests

    [Fact]
    public void Validate_RagTopK_WithZero_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("Rag:TopK", "0", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TopK must be at least 1"));
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    public void Validate_RagMinScore_WithInvalidRange_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("RAG:MinScore", value, "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Score threshold must be between 0 and 1"));
    }

    [Fact]
    public void Validate_RagThreshold_WithInvalidRange_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("Rag:Threshold", "1.5", "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Score threshold must be between 0 and 1"));
    }

    [Theory]
    [InlineData("50")]
    [InlineData("15000")]
    public void Validate_RagMaxChunkSize_WithInvalidRange_ReturnsInvalid(string value)
    {
        // Act
        var result = _validator.Validate("Rag:MaxChunkSize", value, "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxChunkSize must be between 100 and 10000"));
    }

    [Fact]
    public void Validate_RagChunkOverlap_WithNegative_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("Rag:ChunkOverlap", "-10", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ChunkOverlap must be non-negative"));
    }

    #endregion

    #region PDF Rules Tests

    [Fact]
    public void Validate_PdfMaxFileSizeMB_WithZero_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("Pdf:MaxFileSizeMB", "0", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxFileSizeMB must be at least 1"));
    }

    [Fact]
    public void Validate_PdfTimeoutSeconds_WithZero_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("PDF:TimeoutSeconds", "0", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TimeoutSeconds must be at least 1"));
    }

    [Fact]
    public void Validate_PdfQualityThreshold_WithInvalidRange_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("Pdf:QualityThreshold", "1.5", "double");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Quality threshold must be between 0 and 1"));
    }

    #endregion

    #region Feature Flag Rules Tests

    [Fact]
    public void Validate_FeatureFlag_WithBoolean_ReturnsValid()
    {
        // Act
        var result = _validator.Validate("Features.NewDashboard", "true", "bool");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FeatureFlag_WithNonBoolean_ReturnsInvalid()
    {
        // Act
        var result = _validator.Validate("Features.Enabled", "yes", "string");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Feature flag") && e.Contains("must be a boolean"));
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_WhenValid_HasEmptyErrors()
    {
        // Act
        var result = _validator.Validate("TestKey", "test", "string");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_WhenInvalid_HasErrors()
    {
        // Act
        var result = _validator.Validate("TestKey", "not-a-number", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion
}
