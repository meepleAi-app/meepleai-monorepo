using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RequestDeleteSharedGameCommandValidatorTests
{
    private readonly RequestDeleteSharedGameCommandValidator _validator;

    public RequestDeleteSharedGameCommandValidatorTests()
    {
        _validator = new RequestDeleteSharedGameCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var command = new RequestDeleteSharedGameCommand(Guid.NewGuid(), Guid.NewGuid(), "Duplicate entry");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_FailsValidation()
    {
        var command = new RequestDeleteSharedGameCommand(Guid.Empty, Guid.NewGuid(), "Reason");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Validate_WithEmptyRequestedBy_FailsValidation()
    {
        var command = new RequestDeleteSharedGameCommand(Guid.NewGuid(), Guid.Empty, "Reason");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestedBy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyReason_FailsValidation(string reason)
    {
        var command = new RequestDeleteSharedGameCommand(Guid.NewGuid(), Guid.NewGuid(), reason);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Validate_WithReasonTooLong_FailsValidation()
    {
        var longReason = new string('a', 1001);
        var command = new RequestDeleteSharedGameCommand(Guid.NewGuid(), Guid.NewGuid(), longReason);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}