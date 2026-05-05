using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Unit tests for <see cref="UpdateCertificationThresholdsValidator"/>
/// (ADR-051 Sprint 2 / Task 14). Surface invariants mirror the
/// <c>CertificationThresholds.Create</c> value-object factory bounds; the validator
/// keeps endpoint responses as 400 ValidationProblem rather than letting an
/// out-of-range request escalate to a 500 from the factory's <c>ArgumentException</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UpdateCertificationThresholdsValidatorTests
{
    private readonly UpdateCertificationThresholdsValidator _validator = new();

    private static UpdateCertificationThresholdsCommand BuildValid() => new(
        MinCoveragePct: 70m,
        MaxPageTolerance: 10,
        MinBggMatchPct: 80m,
        MinOverallScore: 60m,
        UserId: Guid.NewGuid());

    [Fact]
    public async Task Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var result = await _validator.TestValidateAsync(BuildValid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // -------- MinCoveragePct (0..100) --------

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public async Task Validate_MinCoverageOutOfRange_ShouldHaveError(decimal value)
    {
        var cmd = BuildValid() with { MinCoveragePct = value };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MinCoveragePct);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task Validate_MinCoverageBoundaryValues_ShouldNotHaveError(decimal value)
    {
        var cmd = BuildValid() with { MinCoveragePct = value };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MinCoveragePct);
    }

    // -------- MaxPageTolerance (>= 0) --------

    [Fact]
    public async Task Validate_MaxPageToleranceNegative_ShouldHaveError()
    {
        var cmd = BuildValid() with { MaxPageTolerance = -1 };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MaxPageTolerance);
    }

    [Fact]
    public async Task Validate_MaxPageToleranceZero_ShouldNotHaveError()
    {
        // Zero tolerance is a legitimate strict-mode setting; explicitly allowed.
        var cmd = BuildValid() with { MaxPageTolerance = 0 };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPageTolerance);
    }

    // -------- MinBggMatchPct (0..100) --------

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public async Task Validate_MinBggMatchOutOfRange_ShouldHaveError(decimal value)
    {
        var cmd = BuildValid() with { MinBggMatchPct = value };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MinBggMatchPct);
    }

    // -------- MinOverallScore (0..100) --------

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Validate_MinOverallScoreOutOfRange_ShouldHaveError(decimal value)
    {
        var cmd = BuildValid() with { MinOverallScore = value };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.MinOverallScore);
    }

    // -------- UserId --------

    [Fact]
    public async Task Validate_EmptyUserId_ShouldHaveError()
    {
        var cmd = BuildValid() with { UserId = Guid.Empty };
        var result = await _validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
