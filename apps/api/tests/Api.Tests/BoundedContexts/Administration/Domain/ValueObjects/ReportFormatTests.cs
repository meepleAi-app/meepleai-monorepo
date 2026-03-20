using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Tests for the ReportFormat enum and its extension methods.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReportFormatTests
{
    #region Enum Value Tests

    [Fact]
    public void ReportFormat_CsvValue_IsCorrect()
    {
        // Assert
        ((int)ReportFormat.Csv).Should().Be(1);
    }

    [Fact]
    public void ReportFormat_JsonValue_IsCorrect()
    {
        // Assert
        ((int)ReportFormat.Json).Should().Be(2);
    }

    [Fact]
    public void ReportFormat_PdfValue_IsCorrect()
    {
        // Assert
        ((int)ReportFormat.Pdf).Should().Be(3);
    }

    #endregion

    #region ToFileExtension Tests

    [Theory]
    [InlineData(ReportFormat.Csv, "csv")]
    [InlineData(ReportFormat.Json, "json")]
    [InlineData(ReportFormat.Pdf, "pdf")]
    public void ToFileExtension_ReturnsCorrectExtension(ReportFormat format, string expected)
    {
        // Act
        var result = format.ToFileExtension();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToFileExtension_WithInvalidFormat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidFormat = (ReportFormat)999;

        // Act
        var action = () => invalidFormat.ToFileExtension();

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region ToContentType Tests

    [Theory]
    [InlineData(ReportFormat.Csv, "text/csv")]
    [InlineData(ReportFormat.Json, "application/json")]
    [InlineData(ReportFormat.Pdf, "application/pdf")]
    public void ToContentType_ReturnsCorrectContentType(ReportFormat format, string expected)
    {
        // Act
        var result = format.ToContentType();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToContentType_WithInvalidFormat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidFormat = (ReportFormat)999;

        // Act
        var action = () => invalidFormat.ToContentType();

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void ReportFormat_HasThreeValues()
    {
        // Arrange
        var values = Enum.GetValues<ReportFormat>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Fact]
    public void ReportFormat_AllValuesHaveFileExtensions()
    {
        // Arrange
        var formats = Enum.GetValues<ReportFormat>();

        // Act & Assert
        foreach (var format in formats)
        {
            var extension = format.ToFileExtension();
            extension.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ReportFormat_AllValuesHaveContentTypes()
    {
        // Arrange
        var formats = Enum.GetValues<ReportFormat>();

        // Act & Assert
        foreach (var format in formats)
        {
            var contentType = format.ToContentType();
            contentType.Should().NotBeNullOrEmpty();
            contentType.Should().Contain("/");
        }
    }

    #endregion
}