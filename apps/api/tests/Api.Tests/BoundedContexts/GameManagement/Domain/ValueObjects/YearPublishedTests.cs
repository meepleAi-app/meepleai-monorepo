using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the YearPublished value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 19
/// </summary>
[Trait("Category", "Unit")]
public sealed class YearPublishedTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidYear_CreatesYearPublished()
    {
        // Act
        var year = new YearPublished(2020);

        // Assert
        year.Value.Should().Be(2020);
    }

    [Fact]
    public void Constructor_WithMinYear1000_Succeeds()
    {
        // Act
        var year = new YearPublished(1000);

        // Assert
        year.Value.Should().Be(1000);
    }

    [Fact]
    public void Constructor_WithYearBefore1000_ThrowsValidationException()
    {
        // Act
        var action = () => new YearPublished(999);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Publication year cannot be before 1000*");
    }

    [Fact]
    public void Constructor_WithCurrentYear_Succeeds()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;

        // Act
        var year = new YearPublished(currentYear);

        // Assert
        year.Value.Should().Be(currentYear);
    }

    [Fact]
    public void Constructor_WithFutureYearWithin5Years_Succeeds()
    {
        // Arrange
        var futureYear = DateTime.UtcNow.Year + 5;

        // Act
        var year = new YearPublished(futureYear);

        // Assert
        year.Value.Should().Be(futureYear);
    }

    [Fact]
    public void Constructor_WithYearMoreThan5YearsInFuture_ThrowsValidationException()
    {
        // Arrange
        var farFutureYear = DateTime.UtcNow.Year + 6;

        // Act
        var action = () => new YearPublished(farFutureYear);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Publication year cannot be after*");
    }

    [Fact]
    public void Constructor_WithHistoricalYear_Succeeds()
    {
        // Act - Chess-like games from medieval times
        var year = new YearPublished(1475);

        // Assert
        year.Value.Should().Be(1475);
    }

    #endregion

    #region IsClassic Tests

    [Fact]
    public void IsClassic_WithYearBefore2000_ReturnsTrue()
    {
        // Arrange
        var year = new YearPublished(1995);

        // Assert
        year.IsClassic.Should().BeTrue();
    }

    [Fact]
    public void IsClassic_WithYear1999_ReturnsTrue()
    {
        // Arrange
        var year = new YearPublished(1999);

        // Assert
        year.IsClassic.Should().BeTrue();
    }

    [Fact]
    public void IsClassic_WithYear2000_ReturnsFalse()
    {
        // Arrange
        var year = new YearPublished(2000);

        // Assert
        year.IsClassic.Should().BeFalse();
    }

    [Fact]
    public void IsClassic_WithModernYear_ReturnsFalse()
    {
        // Arrange
        var year = new YearPublished(2020);

        // Assert
        year.IsClassic.Should().BeFalse();
    }

    #endregion

    #region IsModern Tests

    [Fact]
    public void IsModern_WithYear2000_ReturnsTrue()
    {
        // Arrange
        var year = new YearPublished(2000);

        // Assert
        year.IsModern.Should().BeTrue();
    }

    [Fact]
    public void IsModern_WithYearAfter2000_ReturnsTrue()
    {
        // Arrange
        var year = new YearPublished(2023);

        // Assert
        year.IsModern.Should().BeTrue();
    }

    [Fact]
    public void IsModern_WithYearBefore2000_ReturnsFalse()
    {
        // Arrange
        var year = new YearPublished(1999);

        // Assert
        year.IsModern.Should().BeFalse();
    }

    #endregion

    #region IsRecent Tests

    [Fact]
    public void IsRecent_WithCurrentYear_ReturnsTrue()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var year = new YearPublished(currentYear);

        // Assert
        year.IsRecent.Should().BeTrue();
    }

    [Fact]
    public void IsRecent_WithYearWithin3Years_ReturnsTrue()
    {
        // Arrange
        var recentYear = DateTime.UtcNow.Year - 2;
        var year = new YearPublished(recentYear);

        // Assert
        year.IsRecent.Should().BeTrue();
    }

    [Fact]
    public void IsRecent_WithYear3YearsAgo_ReturnsTrue()
    {
        // Arrange
        var threeYearsAgo = DateTime.UtcNow.Year - 3;
        var year = new YearPublished(threeYearsAgo);

        // Assert
        year.IsRecent.Should().BeTrue();
    }

    [Fact]
    public void IsRecent_WithYear4YearsAgo_ReturnsFalse()
    {
        // Arrange
        var fourYearsAgo = DateTime.UtcNow.Year - 4;
        var year = new YearPublished(fourYearsAgo);

        // Assert
        year.IsRecent.Should().BeFalse();
    }

    [Fact]
    public void IsRecent_WithOldYear_ReturnsFalse()
    {
        // Arrange
        var year = new YearPublished(2000);

        // Assert
        year.IsRecent.Should().BeFalse();
    }

    #endregion

    #region Age Tests

    [Fact]
    public void Age_WithCurrentYear_ReturnsZero()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var year = new YearPublished(currentYear);

        // Assert
        year.Age.Should().Be(0);
    }

    [Fact]
    public void Age_WithPreviousYear_ReturnsCorrectAge()
    {
        // Arrange
        var fiveYearsAgo = DateTime.UtcNow.Year - 5;
        var year = new YearPublished(fiveYearsAgo);

        // Assert
        year.Age.Should().Be(5);
    }

    [Fact]
    public void Age_WithHistoricalYear_ReturnsCorrectAge()
    {
        // Arrange
        var year = new YearPublished(2000);

        // Assert
        var expectedAge = DateTime.UtcNow.Year - 2000;
        year.Age.Should().Be(expectedAge);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameYear_ReturnsTrue()
    {
        // Arrange
        var year1 = new YearPublished(2020);
        var year2 = new YearPublished(2020);

        // Assert
        year1.Should().Be(year2);
    }

    [Fact]
    public void Equals_WithDifferentYear_ReturnsFalse()
    {
        // Arrange
        var year1 = new YearPublished(2020);
        var year2 = new YearPublished(2021);

        // Assert
        year1.Should().NotBe(year2);
    }

    [Fact]
    public void GetHashCode_WithSameYear_ReturnsSameHash()
    {
        // Arrange
        var year1 = new YearPublished(2020);
        var year2 = new YearPublished(2020);

        // Assert
        year1.GetHashCode().Should().Be(year2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsYearAsString()
    {
        // Arrange
        var year = new YearPublished(2020);

        // Act
        var result = year.ToString();

        // Assert
        result.Should().Be("2020");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        // Arrange
        var year = new YearPublished(2020);

        // Act
        int value = year;

        // Assert
        value.Should().Be(2020);
    }

    #endregion

    #region ToInt32 Tests

    [Fact]
    public void ToInt32_ThrowsNotSupportedException()
    {
        // Arrange
        var year = new YearPublished(2020);

        // Act
        var action = () => year.ToInt32();

        // Assert
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Use implicit conversion to int instead*");
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void IsClassicAndIsModern_AreMutuallyExclusive()
    {
        // Arrange - test various years
        var years = new[] { 1475, 1999, 2000, 2020 };

        foreach (var yearValue in years)
        {
            var year = new YearPublished(yearValue);

            // Assert - a year should be either classic or modern, not both
            (year.IsClassic && year.IsModern).Should().BeFalse(
                $"Year {yearValue} should not be both classic and modern");
            (year.IsClassic || year.IsModern).Should().BeTrue(
                $"Year {yearValue} should be either classic or modern");
        }
    }

    #endregion
}
