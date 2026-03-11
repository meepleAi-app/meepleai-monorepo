using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for UserRegionDetector.
/// Issue #27: Extracts region hint from Accept-Language header.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class UserRegionDetectorTests
{
    [Theory]
    [InlineData("en-US,en;q=0.9,it;q=0.8", "en-US")]
    [InlineData("it-IT,it;q=0.9,en-US;q=0.8", "it-IT")]
    [InlineData("de-DE", "de-DE")]
    [InlineData("fr", "fr")]
    [InlineData("zh-CN;q=1.0,en-US;q=0.5", "zh-CN")]
    public void DetectRegion_ReturnsFirstLanguageTag(string acceptLanguage, string expected)
    {
        // Arrange
        var detector = CreateDetector(acceptLanguage);

        // Act
        var result = detector.DetectRegion();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DetectRegion_ReturnsNull_WhenHeaderMissing(string? acceptLanguage)
    {
        // Arrange
        var detector = CreateDetector(acceptLanguage);

        // Act
        var result = detector.DetectRegion();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DetectRegion_ReturnsNull_WhenNoHttpContext()
    {
        // Arrange
        var accessor = new HttpContextAccessor { HttpContext = null };
        var detector = new UserRegionDetector(accessor);

        // Act
        var result = detector.DetectRegion();

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("en_US")]
    [InlineData("a")]
    [InlineData("this-is-way-too-long-value")]
    public void DetectRegion_ReturnsNull_ForInvalidTags(string acceptLanguage)
    {
        // Arrange
        var detector = CreateDetector(acceptLanguage);

        // Act
        var result = detector.DetectRegion();

        // Assert
        Assert.Null(result);
    }

    private static UserRegionDetector CreateDetector(string? acceptLanguage)
    {
        var context = new DefaultHttpContext();
        if (acceptLanguage != null)
            context.Request.Headers.AcceptLanguage = acceptLanguage;

        var accessor = new HttpContextAccessor { HttpContext = context };
        return new UserRegionDetector(accessor);
    }
}
