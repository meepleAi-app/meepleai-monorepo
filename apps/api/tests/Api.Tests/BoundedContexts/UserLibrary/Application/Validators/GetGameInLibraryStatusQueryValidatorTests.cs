using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Unit tests for GetGameInLibraryStatusQueryValidator.
/// Issue #4259: Collection Quick Actions for MeepleCard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGameInLibraryStatusQueryValidatorTests
{
    private readonly GetGameInLibraryStatusQueryValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyUserId_ReturnsValidationError()
    {
        // Arrange
        var query = new GetGameInLibraryStatusQuery(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.UserId));
    }

    [Fact]
    public void Validate_WithEmptyGameId_ReturnsValidationError()
    {
        // Arrange
        var query = new GetGameInLibraryStatusQuery(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(query.GameId));
    }

    [Fact]
    public void Validate_WithBothIdsEmpty_ReturnsMultipleValidationErrors()
    {
        // Arrange
        var query = new GetGameInLibraryStatusQuery(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Validate_WithValidIds_ReturnsNoErrors()
    {
        // Arrange
        var query = new GetGameInLibraryStatusQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
