using Api.Middleware;
using Microsoft.Extensions.Options;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Middleware;

/// <summary>
/// Tests for SecurityHeadersOptionsValidator (Issue #1447).
/// Validates configuration validation at startup.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SecurityHeadersOptionsValidatorTests
{
    private readonly SecurityHeadersOptionsValidator _validator;

    public SecurityHeadersOptionsValidatorTests()
    {
        _validator = new SecurityHeadersOptionsValidator();
    }

    [Fact]
    public void Validate_ValidDefaultOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new SecurityHeadersOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NullCspPolicy_WhenEnabled_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableCsp = true,
            CspPolicy = null!
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("CSP policy cannot be null or empty when CSP is enabled", result.FailureMessage);
    }

    [Fact]
    public void Validate_EmptyCspPolicy_WhenEnabled_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableCsp = true,
            CspPolicy = "   "
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("CSP policy cannot be null or empty when CSP is enabled", result.FailureMessage);
    }

    [Fact]
    public void Validate_HstsPolicyWithoutMaxAge_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableHsts = true,
            HstsPolicy = "includeSubDomains; preload"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("HSTS policy must contain 'max-age=' directive", result.FailureMessage);
    }

    [Fact]
    public void Validate_HstsPreloadWithoutIncludeSubDomains_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableHsts = true,
            HstsPolicy = "max-age=31536000; preload"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("HSTS preload requires includeSubDomains directive", result.FailureMessage);
    }

    [Fact]
    public void Validate_HstsPreloadWithShortMaxAge_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableHsts = true,
            HstsPolicy = "max-age=300; includeSubDomains; preload"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("HSTS preload requires max-age of at least 31536000", result.FailureMessage);
    }

    [Fact]
    public void Validate_ValidHstsPreload_ReturnsSuccess()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableHsts = true,
            HstsPolicy = "max-age=31536000; includeSubDomains; preload"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidXFrameOptions_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableXFrameOptions = true,
            XFrameOptionsPolicy = "INVALID"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("X-Frame-Options must be DENY, SAMEORIGIN, or ALLOW-FROM", result.FailureMessage);
    }

    [Theory]
    [InlineData("DENY")]
    [InlineData("deny")]
    [InlineData("SAMEORIGIN")]
    [InlineData("sameorigin")]
    [InlineData("ALLOW-FROM https://example.com")]
    public void Validate_ValidXFrameOptions_ReturnsSuccess(string policy)
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableXFrameOptions = true,
            XFrameOptionsPolicy = policy
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidXContentTypeOptions_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableXContentTypeOptions = true,
            XContentTypeOptionsPolicy = "invalid"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("X-Content-Type-Options must be 'nosniff'", result.FailureMessage);
    }

    [Theory]
    [InlineData("nosniff")]
    [InlineData("NOSNIFF")]
    [InlineData("NoSniff")]
    public void Validate_ValidXContentTypeOptions_ReturnsSuccess(string policy)
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableXContentTypeOptions = true,
            XContentTypeOptionsPolicy = policy
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidReferrerPolicy_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableReferrerPolicy = true,
            ReferrerPolicyValue = "invalid-policy"
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("Referrer-Policy value 'invalid-policy' is not a valid policy", result.FailureMessage);
    }

    [Theory]
    [InlineData("no-referrer")]
    [InlineData("no-referrer-when-downgrade")]
    [InlineData("origin")]
    [InlineData("origin-when-cross-origin")]
    [InlineData("same-origin")]
    [InlineData("strict-origin")]
    [InlineData("strict-origin-when-cross-origin")]
    [InlineData("unsafe-url")]
    public void Validate_ValidReferrerPolicy_ReturnsSuccess(string policy)
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableReferrerPolicy = true,
            ReferrerPolicyValue = policy
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NullPermissionsPolicy_WhenEnabled_ReturnsFail()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnablePermissionsPolicy = true,
            PermissionsPolicyValue = null!
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("Permissions-Policy cannot be null or empty when enabled", result.FailureMessage);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var options = new SecurityHeadersOptions
        {
            EnableCsp = true,
            CspPolicy = null!,
            EnableHsts = true,
            HstsPolicy = "invalid",
            EnableXFrameOptions = true,
            XFrameOptionsPolicy = null!
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains("CSP policy cannot be null or empty", result.FailureMessage);
        Assert.Contains("HSTS policy must contain 'max-age='", result.FailureMessage);
        Assert.Contains("X-Frame-Options policy cannot be null or empty", result.FailureMessage);
    }

    [Fact]
    public void Validate_DisabledHeaders_NotValidated()
    {
        // Arrange - All headers disabled with invalid policies
        var options = new SecurityHeadersOptions
        {
            EnableCsp = false,
            CspPolicy = null!,
            EnableHsts = false,
            HstsPolicy = null!,
            EnableXFrameOptions = false,
            XFrameOptionsPolicy = null!
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert - Should succeed because headers are disabled
        Assert.True(result.Succeeded);
    }
}
