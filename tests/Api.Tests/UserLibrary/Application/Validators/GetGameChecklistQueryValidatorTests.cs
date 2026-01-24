using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Validators;

public sealed class GetGameChecklistQueryValidatorTests
{
    private readonly GetGameChecklistQueryValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        // Arrange
        var query = new GetGameChecklistQuery(Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyGameId_Fails()
    {
        // Arrange
        var query = new GetGameChecklistQuery(Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GameId");
    }

    [Fact]
    public void Validate_IncludeWizardFlag_DoesNotAffectValidation()
    {
        // Arrange
        var query1 = new GetGameChecklistQuery(Guid.NewGuid(), IncludeWizard: true);
        var query2 = new GetGameChecklistQuery(Guid.NewGuid(), IncludeWizard: false);

        // Act
        var result1 = _validator.Validate(query1);
        var result2 = _validator.Validate(query2);

        // Assert
        result1.IsValid.Should().BeTrue();
        result2.IsValid.Should().BeTrue();
    }
}
