using Api.SharedKernel.Domain.Results;
using Api.SharedKernel.Domain.Validation;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.SharedKernel.Domain.Validation;

[Trait("Category", TestCategories.Unit)]

public class ValidationExtensionsTests
{
    [Fact]
    public void NotNullOrWhiteSpace_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.NotNullOrWhiteSpace("param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhiteSpace_WithInvalidString_ReturnsFailure(string? value)
    {
        // Act
        var result = value.NotNullOrWhiteSpace("param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("param");
    }

    [Fact]
    public void MinLength_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.MinLength(3, "param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void MinLength_WithShortString_ReturnsFailure()
    {
        // Arrange
        var value = "ab";

        // Act
        var result = value.MinLength(3, "param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("at least 3");
    }

    [Fact]
    public void MaxLength_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.MaxLength(10, "param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void MaxLength_WithLongString_ReturnsFailure()
    {
        // Arrange
        var value = "this is a very long string";

        // Act
        var result = value.MaxLength(5, "param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("not exceed 5");
    }

    [Fact]
    public void MatchesPattern_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var value = "test123";

        // Act
        var result = value.MatchesPattern(@"^[a-z0-9]+$", "param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void MatchesPattern_WithInvalidString_ReturnsFailure()
    {
        // Arrange
        var value = "test@123";

        // Act
        var result = value.MatchesPattern(@"^[a-z0-9]+$", "param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("does not match");
    }
    [Fact]
    public void NotEmpty_WithValidGuid_ReturnsSuccess()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var result = value.NotEmpty("param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void NotEmpty_WithEmptyGuid_ReturnsFailure()
    {
        // Arrange
        var value = Guid.Empty;

        // Act
        var result = value.NotEmpty("param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("param");
    }

    [Fact]
    public void NotNullOrEmpty_WithValidNullableGuid_ReturnsSuccess()
    {
        // Arrange
        Guid? value = Guid.NewGuid();

        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value.Value);
    }

    [Theory]
    [InlineData(null)]
    public void NotNullOrEmpty_WithNullGuid_ReturnsFailure(Guid? value)
    {
        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }
    [Fact]
    public void GreaterThan_WithValidValue_ReturnsSuccess()
    {
        // Arrange
        var value = 10;

        // Act
        var result = value.GreaterThan(5, "param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void GreaterThan_WithInvalidValue_ReturnsFailure()
    {
        // Arrange
        var value = 3;

        // Act
        var result = value.GreaterThan(5, "param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("greater than 5");
    }

    [Fact]
    public void InRange_WithValidValue_ReturnsSuccess()
    {
        // Arrange
        var value = 5;

        // Act
        var result = value.InRange(1, 10, "param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void InRange_WithValueBelowMin_ReturnsFailure()
    {
        // Arrange
        var value = 0;

        // Act
        var result = value.InRange(1, 10, "param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("between 1 and 10");
    }
    [Fact]
    public void NotNullOrEmpty_Collection_WithValidCollection_ReturnsSuccess()
    {
        // Arrange
        var value = new[] { 1, 2, 3 };

        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(value);
    }

    [Fact]
    public void NotNullOrEmpty_Collection_WithEmptyCollection_ReturnsFailure()
    {
        // Arrange
        var value = Array.Empty<int>();

        // Act
        var result = value.NotNullOrEmpty("param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void HasCount_WithCorrectCount_ReturnsSuccess()
    {
        // Arrange
        var value = new[] { 1, 2, 3 };

        // Act
        var result = value.HasCount(3, "param");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void HasCount_WithIncorrectCount_ReturnsFailure()
    {
        // Arrange
        var value = new[] { 1, 2, 3 };

        // Act
        var result = value.HasCount(5, "param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("exactly 5");
    }
    [Fact]
    public void NotNull_WithValidObject_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.NotNull("param");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void NotNull_WithNullObject_ReturnsFailure()
    {
        // Arrange
        string? value = null;

        // Act
        var result = value.NotNull("param");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }
    [Fact]
    public void Then_WithSuccessfulValidations_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value
            .NotNullOrWhiteSpace("param")
            .Then(v => v.MinLength(2, "param"))
            .Then(v => v.MaxLength(10, "param"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void Then_WithFailedValidation_ReturnsFirstFailure()
    {
        // Arrange
        var value = "a";

        // Act
        var result = value
            .NotNullOrWhiteSpace("param")
            .Then(v => v.MinLength(5, "param"))
            .Then(v => v.MaxLength(10, "param"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("at least 5");
    }

    [Fact]
    public void Must_WithValidPredicate_ReturnsSuccess()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.Must(v => v.Contains("es"), "Must contain 'es'");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Must_WithInvalidPredicate_ReturnsFailure()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.Must(v => v.Contains("xyz"), "Must contain 'xyz'");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Must contain 'xyz'");
    }
}

