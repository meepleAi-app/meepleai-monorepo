using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;

/// <summary>
/// Tests for the WorkflowUrl value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkflowUrlTests
{
    #region Constructor Tests

    [Theory]
    [InlineData("https://n8n.example.com")]
    [InlineData("http://localhost:5678")]
    [InlineData("https://workflow.company.io/api")]
    [InlineData("http://192.168.1.100:8080")]
    public void Constructor_WithValidHttpUrl_CreatesInstance(string url)
    {
        // Act
        var workflowUrl = new WorkflowUrl(url);

        // Assert
        workflowUrl.Value.Should().Be(url);
    }

    [Theory]
    [InlineData("  https://n8n.example.com  ", "https://n8n.example.com")]
    [InlineData("\thttps://example.com\t", "https://example.com")]
    public void Constructor_WithWhitespace_TrimsUrl(string input, string expected)
    {
        // Act
        var workflowUrl = new WorkflowUrl(input);

        // Assert
        workflowUrl.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyOrNull_ThrowsValidationException(string? url)
    {
        // Act
        var action = () => new WorkflowUrl(url!);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("Workflow URL cannot be empty");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    public void Constructor_WithInvalidUrlFormat_ThrowsValidationException(string url)
    {
        // Act
        var action = () => new WorkflowUrl(url);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage($"Invalid URL format: {url}");
    }

    [Theory]
    [InlineData("/api/workflow")] // Relative path - may parse as file:// URI on Linux
    public void Constructor_WithRelativePath_ThrowsValidationException(string url)
    {
        // Act
        var action = () => new WorkflowUrl(url);

        // Assert - Accept either message due to cross-platform URI parsing differences
        // On Windows: fails at Uri.TryCreate (relative path) -> "Invalid URL format"
        // On Linux: parses as file:// URI -> "URL must use HTTP or HTTPS protocol"
        action.Should().Throw<ValidationException>()
            .Where(ex => ex.Message.Contains("Invalid URL format") || ex.Message.Contains("URL must use HTTP or HTTPS protocol"));
    }

    [Theory]
    [InlineData("ftp://files.example.com")]
    [InlineData("file:///C:/path/to/file")]
    [InlineData("mailto:test@example.com")]
    [InlineData("ws://websocket.example.com")]
    public void Constructor_WithNonHttpProtocol_ThrowsValidationException(string url)
    {
        // Act
        var action = () => new WorkflowUrl(url);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("URL must use HTTP or HTTPS protocol");
    }

    #endregion

    #region ToUri Tests

    [Fact]
    public void ToUri_ReturnsValidUri()
    {
        // Arrange
        var url = "https://n8n.example.com:5678/api";
        var workflowUrl = new WorkflowUrl(url);

        // Act
        var uri = workflowUrl.ToUri();

        // Assert
        uri.Should().NotBeNull();
        uri.Scheme.Should().Be("https");
        uri.Host.Should().Be("n8n.example.com");
        uri.Port.Should().Be(5678);
        uri.AbsolutePath.Should().Be("/api");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameUrl_ReturnsTrue()
    {
        // Arrange
        var url1 = new WorkflowUrl("https://n8n.example.com");
        var url2 = new WorkflowUrl("https://n8n.example.com");

        // Act & Assert
        url1.Should().Be(url2);
    }

    [Fact]
    public void Equals_WithDifferentCase_ReturnsTrue()
    {
        // Arrange (URL comparison is case-insensitive for domain)
        var url1 = new WorkflowUrl("https://N8N.EXAMPLE.COM");
        var url2 = new WorkflowUrl("https://n8n.example.com");

        // Act & Assert
        url1.Should().Be(url2);
    }

    [Fact]
    public void Equals_WithDifferentUrls_ReturnsFalse()
    {
        // Arrange
        var url1 = new WorkflowUrl("https://n8n.example.com");
        var url2 = new WorkflowUrl("https://other.example.com");

        // Act & Assert
        url1.Should().NotBe(url2);
    }

    [Fact]
    public void GetHashCode_SameUrls_ReturnsSameHash()
    {
        // Arrange
        var url1 = new WorkflowUrl("https://n8n.example.com");
        var url2 = new WorkflowUrl("https://n8n.example.com");

        // Act & Assert
        url1.GetHashCode().Should().Be(url2.GetHashCode());
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var workflowUrl = new WorkflowUrl("https://n8n.example.com");

        // Act
        string value = workflowUrl;

        // Assert
        value.Should().Be("https://n8n.example.com");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsUrlValue()
    {
        // Arrange
        var url = "https://n8n.example.com/api";
        var workflowUrl = new WorkflowUrl(url);

        // Act
        var result = workflowUrl.ToString();

        // Assert
        result.Should().Be(url);
    }

    #endregion
}
