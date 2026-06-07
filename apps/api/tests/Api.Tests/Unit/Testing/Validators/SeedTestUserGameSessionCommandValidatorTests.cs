using Api.BoundedContexts.Testing.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

/// <summary>
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION, DEC-B-5) — Unit validator coverage
/// for <see cref="SeedTestUserGameSessionCommandValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1929")]
public class SeedTestUserGameSessionCommandValidatorTests
{
    private readonly SeedTestUserGameSessionCommandValidator _sut = new();

    private static readonly Guid ValidLibraryEntryId = Guid.NewGuid();
    private const string ValidTestRunId = "e2e-usergamesess01-1717603200000";

    [Fact]
    public void Validate_ValidCommand_PassesAll()
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = ValidTestRunId,
            UserLibraryEntryId = ValidLibraryEntryId,
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidCommandWithAllOptionals_PassesAll()
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = ValidTestRunId,
            UserLibraryEntryId = ValidLibraryEntryId,
            PlayedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 90,
            DidWin = true,
            Players = "Anna, Marco",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("e2e-no-epoch-")]
    [InlineData("noe2e-prefix-1717603200000")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = testRunId,
            UserLibraryEntryId = ValidLibraryEntryId,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }

    [Fact]
    public void Validate_EmptyUserLibraryEntryId_FailsValidation()
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = ValidTestRunId,
            UserLibraryEntryId = Guid.Empty,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.UserLibraryEntryId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_DurationMinutesZeroOrNegative_FailsValidation(int duration)
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = ValidTestRunId,
            UserLibraryEntryId = ValidLibraryEntryId,
            DurationMinutes = duration,
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DurationMinutes!.Value);
    }

    [Fact]
    public void Validate_DurationMinutesNull_PassesAll()
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = ValidTestRunId,
            UserLibraryEntryId = ValidLibraryEntryId,
            DurationMinutes = null,
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_AllOptionalFieldsNull_PassesAll()
    {
        var cmd = new SeedTestUserGameSessionCommand
        {
            TestRunId = ValidTestRunId,
            UserLibraryEntryId = ValidLibraryEntryId,
            PlayedAt = null,
            DurationMinutes = null,
            DidWin = null,
            Players = null,
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}
