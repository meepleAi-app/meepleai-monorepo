using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateGameErrataCommandValidatorTests
{
    private readonly UpdateGameErrataCommandValidator _validator;

    public UpdateGameErrataCommandValidatorTests()
    {
        _validator = new UpdateGameErrataCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new UpdateGameErrataCommand(Guid.NewGuid(), "Corrected typo", "Page 5", DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyDescription_FailsValidation(string description)
    {
        var command = new UpdateGameErrataCommand(Guid.NewGuid(), description, "Page 1", DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyPageReference_FailsValidation(string pageRef)
    {
        var command = new UpdateGameErrataCommand(Guid.NewGuid(), "Description", pageRef, DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PageReference);
    }

    [Fact]
    public void Validate_WithEmptyErrataId_FailsValidation()
    {
        var command = new UpdateGameErrataCommand(Guid.Empty, "Description", "Page 1", DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ErrataId);
    }

    [Fact]
    public void Validate_WithFuturePublishedDate_FailsValidation()
    {
        var command = new UpdateGameErrataCommand(Guid.NewGuid(), "Description", "Page 1", DateTime.UtcNow.AddDays(1));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PublishedDate);
    }

    [Fact]
    public void Validate_WithPageReferenceTooLong_FailsValidation()
    {
        var longPageRef = new string('a', 101);
        var command = new UpdateGameErrataCommand(Guid.NewGuid(), "Description", longPageRef, DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PageReference);
    }
}
