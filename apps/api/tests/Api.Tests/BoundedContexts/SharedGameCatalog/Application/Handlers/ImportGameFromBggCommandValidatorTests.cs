using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ImportGameFromBggCommandValidatorTests
{
    private readonly ImportGameFromBggCommandValidator _validator;

    public ImportGameFromBggCommandValidatorTests()
    {
        _validator = new ImportGameFromBggCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new ImportGameFromBggCommand(123, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithInvalidBggId_FailsValidation(int bggId)
    {
        var command = new ImportGameFromBggCommand(bggId, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BggId);
    }

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        var command = new ImportGameFromBggCommand(123, Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
