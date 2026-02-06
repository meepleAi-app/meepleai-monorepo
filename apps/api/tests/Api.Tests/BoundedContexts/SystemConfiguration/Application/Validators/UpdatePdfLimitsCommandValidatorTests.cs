using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Tests for UpdatePdfLimitsCommandValidator.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class UpdatePdfLimitsCommandValidatorTests
{
    private readonly UpdatePdfLimitsCommandValidator _validator;

    public UpdatePdfLimitsCommandValidatorTests()
    {
        _validator = new UpdatePdfLimitsCommandValidator();
    }

    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    public async Task Validate_WithValidTier_Passes(string tier)
    {
        // Arrange
        var command = new UpdatePdfLimitsCommand
        {
            Tier = tier,
            MaxPerDay = 10,
            MaxPerWeek = 50,
            MaxPerGame = 5,
            AdminUserId = Guid.NewGuid()
        };

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("gold")]
    public async Task Validate_WithInvalidTier_Fails(string tier)
    {
        // Arrange
        var command = new UpdatePdfLimitsCommand
        {
            Tier = tier,
            MaxPerDay = 10,
            MaxPerWeek = 50,
            MaxPerGame = 5,
            AdminUserId = Guid.NewGuid()
        };

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Tier);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WithInvalidMaxPerDay_Fails(int maxPerDay)
    {
        // Arrange
        var command = new UpdatePdfLimitsCommand
        {
            Tier = "free",
            MaxPerDay = maxPerDay,
            MaxPerWeek = 50,
            MaxPerGame = 5,
            AdminUserId = Guid.NewGuid()
        };

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPerDay);
    }

    [Fact]
    public async Task Validate_WithMaxPerWeekLessThanMaxPerDay_Fails()
    {
        // Arrange
        var command = new UpdatePdfLimitsCommand
        {
            Tier = "normal",
            MaxPerDay = 100,
            MaxPerWeek = 50,
            MaxPerGame = 5,
            AdminUserId = Guid.NewGuid()
        };

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPerWeek);
    }
}
