using Api.BoundedContexts.Testing.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestPlayerCommandValidatorTests
{
    private readonly SeedTestPlayerCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidPlayerCommand_PassesAll()
    {
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.NewGuid(),
            Role = "player",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_GuestWithoutDisplayName_FailsValidation()
    {
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.NewGuid(),
            Role = "guest",
            DisplayName = null,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("HOST")]
    [InlineData("")]
    public void Validate_InvalidRole_FailsValidation(string role)
    {
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.NewGuid(),
            Role = role,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = Guid.NewGuid(),
            Role = "player",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }

    [Fact]
    public void Validate_EmptyGameNightId_FailsValidation()
    {
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.Empty,
            Role = "host",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.GameNightId);
    }
}
