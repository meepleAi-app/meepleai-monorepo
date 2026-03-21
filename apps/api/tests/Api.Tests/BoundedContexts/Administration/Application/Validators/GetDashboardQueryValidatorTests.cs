using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Unit tests for GetDashboardQueryValidator.
/// Issue #3314: User Dashboard Aggregated API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class GetDashboardQueryValidatorTests
{
    private readonly GetDashboardQueryValidator _validator;

    public GetDashboardQueryValidatorTests()
    {
        _validator = new GetDashboardQueryValidator();
    }

    [Fact]
    public void Validate_WithValidUserId_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetDashboardQuery(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_ShouldHaveError()
    {
        // Arrange
        var query = new GetDashboardQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void Validate_WithDefaultUserId_ShouldHaveError()
    {
        // Arrange
        var query = new GetDashboardQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
