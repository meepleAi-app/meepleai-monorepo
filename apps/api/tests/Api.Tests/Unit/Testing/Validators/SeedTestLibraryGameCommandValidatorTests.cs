using Api.BoundedContexts.Testing.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

/// <summary>
/// Issue #1929 Task C Macro 3a (DEC-B-5, DEC-C-8) — Unit validator coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1929")]
public class SeedTestLibraryGameCommandValidatorTests
{
    private readonly SeedTestLibraryGameCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_PassesAll()
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            OwnerEmail = "owner@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("e2e-no-epoch-")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "owner@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }

    [Fact]
    public void Validate_EmptyOwnerEmail_FailsValidation()
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            OwnerEmail = string.Empty,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.OwnerEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("foo@")]
    [InlineData("@bar.com")]
    public void Validate_InvalidOwnerEmailFormat_FailsValidation(string email)
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            OwnerEmail = email,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.OwnerEmail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Validate_MinPlayersOutOfRange_FailsValidation(int minPlayers)
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            OwnerEmail = "owner@e2e.test",
            MinPlayers = minPlayers,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.MinPlayers!.Value);
    }

    [Fact]
    public void Validate_MaxPlayersBelowMinPlayers_FailsValidation()
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            OwnerEmail = "owner@e2e.test",
            MinPlayers = 4,
            MaxPlayers = 2,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.MaxPlayers!.Value);
    }

    [Fact]
    public void Validate_OptionalFieldsNull_PassesAll()
    {
        var cmd = new SeedTestLibraryGameCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            OwnerEmail = "owner@e2e.test",
            Title = null,
            Publisher = null,
            MinPlayers = null,
            MaxPlayers = null,
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}
