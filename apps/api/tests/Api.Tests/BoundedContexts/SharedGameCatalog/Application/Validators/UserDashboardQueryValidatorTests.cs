using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for user dashboard query validators.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UserDashboardQueryValidatorTests
{
    #region GetUserShareRequestsQueryValidator Tests

    [Fact]
    public void GetUserShareRequestsQuery_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.NewGuid(), null, 1, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GetUserShareRequestsQuery_WithEmptyUserId_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.Empty, null, 1, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void GetUserShareRequestsQuery_WithZeroPageNumber_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.NewGuid(), null, 0, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorMessage("PageNumber must be greater than 0");
    }

    [Fact]
    public void GetUserShareRequestsQuery_WithNegativePageNumber_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.NewGuid(), null, -1, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Fact]
    public void GetUserShareRequestsQuery_WithZeroPageSize_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.NewGuid(), null, 1, 0);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage("PageSize must be between 1 and 100");
    }

    [Fact]
    public void GetUserShareRequestsQuery_WithPageSizeOver100_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.NewGuid(), null, 1, 101);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage("PageSize must be between 1 and 100");
    }

    [Fact]
    public void GetUserShareRequestsQuery_WithStatusFilter_ShouldPassValidation()
    {
        // Arrange
        var validator = new GetUserShareRequestsQueryValidator();
        var query = new GetUserShareRequestsQuery(Guid.NewGuid(), ShareRequestStatus.Pending, 1, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region GetUserContributionsQueryValidator Tests

    [Fact]
    public void GetUserContributionsQuery_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var validator = new GetUserContributionsQueryValidator();
        var query = new GetUserContributionsQuery(Guid.NewGuid(), 1, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GetUserContributionsQuery_WithEmptyUserId_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserContributionsQueryValidator();
        var query = new GetUserContributionsQuery(Guid.Empty, 1, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void GetUserContributionsQuery_WithZeroPageNumber_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserContributionsQueryValidator();
        var query = new GetUserContributionsQuery(Guid.NewGuid(), 0, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber)
            .WithErrorMessage("PageNumber must be greater than 0");
    }

    [Fact]
    public void GetUserContributionsQuery_WithPageSizeOver100_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserContributionsQueryValidator();
        var query = new GetUserContributionsQuery(Guid.NewGuid(), 1, 150);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize)
            .WithErrorMessage("PageSize must be between 1 and 100");
    }

    #endregion

    #region GetUserContributionStatsQueryValidator Tests

    [Fact]
    public void GetUserContributionStatsQuery_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var validator = new GetUserContributionStatsQueryValidator();
        var query = new GetUserContributionStatsQuery(Guid.NewGuid());

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GetUserContributionStatsQuery_WithEmptyUserId_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetUserContributionStatsQueryValidator();
        var query = new GetUserContributionStatsQuery(Guid.Empty);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    #endregion
}