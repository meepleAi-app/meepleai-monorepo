using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class YearPublishedTests
{
    [Fact]
    public void YearPublished_WithValidYear_CreatesSuccessfully()
    {
        // Arrange & Act
        var year = new YearPublished(2020);

        // Assert
        Assert.Equal(2020, year.Value);
    }

    [Fact]
    public void YearPublished_IsClassic_Before2000()
    {
        // Arrange
        var classic = new YearPublished(1995);
        var modern = new YearPublished(2010);

        // Act & Assert
        Assert.True(classic.IsClassic);
        Assert.False(modern.IsClassic);
    }

    [Fact]
    public void YearPublished_IsModern_From2000Onwards()
    {
        // Arrange
        var modern = new YearPublished(2020);
        var classic = new YearPublished(1999);

        // Act & Assert
        Assert.True(modern.IsModern);
        Assert.False(classic.IsModern);
    }

    [Fact]
    public void YearPublished_IsRecent_WithinLast3Years()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var recent = new YearPublished(currentYear - 1);
        var old = new YearPublished(currentYear - 5);

        // Act & Assert
        Assert.True(recent.IsRecent);
        Assert.False(old.IsRecent);
    }

    [Fact]
    public void YearPublished_Age_CalculatesCorrectly()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var year = new YearPublished(currentYear - 10);

        // Act
        var age = year.Age;

        // Assert
        Assert.Equal(10, age);
    }

    [Fact]
    public void YearPublished_BelowMinimum_ThrowsValidationException()
    {
        // Act & Assert (MinYear = 1000 for historical games support)
        var exception = Assert.Throws<ValidationException>(() => new YearPublished(999));
        Assert.Contains("cannot be before 1000", exception.Message);
    }

    [Fact]
    public void YearPublished_AboveMaximum_ThrowsValidationException()
    {
        // Arrange
        var futureYear = DateTime.UtcNow.Year + 10;

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new YearPublished(futureYear));
        Assert.Contains("cannot be after", exception.Message);
    }

    [Fact]
    public void YearPublished_ImplicitIntConversion_Works()
    {
        // Arrange
        var year = new YearPublished(2020);

        // Act
        int yearInt = year;

        // Assert
        Assert.Equal(2020, yearInt);
    }

    [Fact]
    public void YearPublished_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var year1 = new YearPublished(2020);
        var year2 = new YearPublished(2020);
        var year3 = new YearPublished(2021);

        // Act & Assert
        Assert.Equal(year1, year2);
        Assert.NotEqual(year1, year3);
    }
}

