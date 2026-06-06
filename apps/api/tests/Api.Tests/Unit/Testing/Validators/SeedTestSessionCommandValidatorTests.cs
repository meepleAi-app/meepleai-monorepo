using Api.BoundedContexts.Testing.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestSessionCommandValidatorTests
{
    private readonly SeedTestSessionCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_PassesAll()
    {
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.NewGuid(),
            IsLive = true,
            ScoreType = "Points",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("e2e-tooshort-1")]
    [InlineData("")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = Guid.NewGuid(),
            IsLive = false,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }

    [Fact]
    public void Validate_EmptyGameNightId_FailsValidation()
    {
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.Empty,
            IsLive = false,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.GameNightId);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("points")]
    public void Validate_InvalidScoreType_FailsValidation(string scoreType)
    {
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.NewGuid(),
            IsLive = false,
            ScoreType = scoreType,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ScoreType);
    }

    [Fact]
    public void Validate_NullScoreType_PassesValidation()
    {
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            GameNightId = Guid.NewGuid(),
            IsLive = false,
            ScoreType = null,
        };
        _sut.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.ScoreType);
    }
}
