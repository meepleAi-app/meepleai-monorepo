using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RejectDeleteRequestCommandValidatorTests
{
    private readonly RejectDeleteRequestCommandValidator _validator;

    public RejectDeleteRequestCommandValidatorTests()
    {
        _validator = new RejectDeleteRequestCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new RejectDeleteRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Not valid reason");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyRequestId_FailsValidation()
    {
        var command = new RejectDeleteRequestCommand(Guid.Empty, Guid.NewGuid(), "Reason");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestId);
    }

    [Fact]
    public void Validate_WithEmptyRejectedBy_FailsValidation()
    {
        var command = new RejectDeleteRequestCommand(Guid.NewGuid(), Guid.Empty, "Reason");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RejectedBy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyReason_FailsValidation(string reason)
    {
        var command = new RejectDeleteRequestCommand(Guid.NewGuid(), Guid.NewGuid(), reason);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}