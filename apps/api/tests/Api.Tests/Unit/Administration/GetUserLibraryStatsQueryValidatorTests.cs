using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Administration;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class GetUserLibraryStatsQueryValidatorTests
{
    private readonly GetUserLibraryStatsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidUserId_Passes()
    {
        // Arrange
        var query = new GetUserLibraryStatsQuery(Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyGuid_Fails()
    {
        // Arrange
        var query = new GetUserLibraryStatsQuery(Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
