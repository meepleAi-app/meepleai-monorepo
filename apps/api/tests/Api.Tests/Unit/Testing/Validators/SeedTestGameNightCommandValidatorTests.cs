using Api.BoundedContexts.Testing.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestGameNightCommandValidatorTests
{
    private readonly SeedTestGameNightCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_PassesAll()
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            Status = "Published",
            OwnerEmail = "ok@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not-e2e-prefix")]
    [InlineData("e2e-tooshort-1")]
    [InlineData("")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = "ok@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("draft")]
    [InlineData("")]
    public void Validate_InvalidStatus_FailsValidation(string status)
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            Status = status,
            OwnerEmail = "ok@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Validate_InvalidEmail_FailsValidation()
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            Status = "Draft",
            OwnerEmail = "not-an-email",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.OwnerEmail);
    }
}
