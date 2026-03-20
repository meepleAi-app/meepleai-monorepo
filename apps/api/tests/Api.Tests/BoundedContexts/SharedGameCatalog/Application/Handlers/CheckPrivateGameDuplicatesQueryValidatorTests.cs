using Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CheckPrivateGameDuplicatesQueryValidatorTests
{
    private readonly CheckPrivateGameDuplicatesQueryValidator _validator;

    public CheckPrivateGameDuplicatesQueryValidatorTests()
    {
        _validator = new CheckPrivateGameDuplicatesQueryValidator();
    }

    [Fact]
    public void Validate_WithValidPrivateGameId_Passes()
    {
        // Arrange
        var query = new CheckPrivateGameDuplicatesQuery(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPrivateGameId_Fails()
    {
        // Arrange
        var query = new CheckPrivateGameDuplicatesQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.PrivateGameId)
            .WithErrorMessage("PrivateGameId is required");
    }
}
