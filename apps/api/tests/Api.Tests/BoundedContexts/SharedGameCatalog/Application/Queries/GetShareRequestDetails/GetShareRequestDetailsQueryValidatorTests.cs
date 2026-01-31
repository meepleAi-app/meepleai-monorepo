using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;

/// <summary>
/// Unit tests for GetShareRequestDetailsQueryValidator.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetShareRequestDetailsQueryValidatorTests
{
    private readonly GetShareRequestDetailsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_ShouldPass()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyShareRequestId_ShouldFail()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ShareRequestId");
    }

    [Fact]
    public void Validate_WithEmptyAdminId_ShouldFail()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AdminId");
    }

    [Fact]
    public void Validate_WithBothIdsEmpty_ShouldFailWithTwoErrors()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}
