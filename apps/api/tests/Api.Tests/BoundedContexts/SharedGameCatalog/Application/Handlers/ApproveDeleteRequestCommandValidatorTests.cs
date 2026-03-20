using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveDeleteRequestCommandValidatorTests
{
    private readonly ApproveDeleteRequestCommandValidator _validator;

    public ApproveDeleteRequestCommandValidatorTests()
    {
        _validator = new ApproveDeleteRequestCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new ApproveDeleteRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Approved");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyRequestId_FailsValidation()
    {
        var command = new ApproveDeleteRequestCommand(Guid.Empty, Guid.NewGuid(), null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestId);
    }

    [Fact]
    public void Validate_WithEmptyApprovedBy_FailsValidation()
    {
        var command = new ApproveDeleteRequestCommand(Guid.NewGuid(), Guid.Empty, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ApprovedBy);
    }
}
