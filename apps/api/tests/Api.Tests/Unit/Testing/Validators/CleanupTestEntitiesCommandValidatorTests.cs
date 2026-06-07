using Api.BoundedContexts.Testing.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class CleanupTestEntitiesCommandValidatorTests
{
    private readonly CleanupTestEntitiesCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_PassesAll()
    {
        var cmd = new CleanupTestEntitiesCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("e2e-short-1")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new CleanupTestEntitiesCommand { TestRunId = testRunId };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }
}
