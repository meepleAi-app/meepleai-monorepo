using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Unit tests for MaterializePdfCoverCommandValidator (Issue #2949 Task 3).
/// Defense-in-depth: PageNumber must be 1-based (> 0) so the handler's
/// PageNumber - 1 conversion never produces a negative index.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class MaterializePdfCoverCommandValidatorTests
{
    private readonly MaterializePdfCoverCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_PageNumber_Is_Positive()
    {
        var command = new MaterializePdfCoverCommand(Guid.NewGuid(), 1, "covers/x/pdf-cover-abc");

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Should_Fail_When_PageNumber_Is_Zero()
    {
        var command = new MaterializePdfCoverCommand(Guid.NewGuid(), 0, "covers/x/pdf-cover-abc");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Fact]
    public void Should_Fail_When_PageNumber_Is_Negative()
    {
        var command = new MaterializePdfCoverCommand(Guid.NewGuid(), -1, "covers/x/pdf-cover-abc");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }
}
