using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Validators;

public sealed class GetGameDetailQueryValidatorTests
{
    private readonly GetGameDetailQueryValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        // Arrange
        var query = new GetGameDetailQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        // Arrange
        var query = new GetGameDetailQuery(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void Validate_EmptyGameId_Fails()
    {
        // Arrange
        var query = new GetGameDetailQuery(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GameId");
    }

    [Fact]
    public void Validate_BothEmpty_Fails()
    {
        // Arrange
        var query = new GetGameDetailQuery(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }
}
