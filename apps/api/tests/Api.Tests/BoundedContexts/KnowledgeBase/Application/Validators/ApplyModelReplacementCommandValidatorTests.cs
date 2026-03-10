using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for ApplyModelReplacementCommandValidator.
/// Issue #5499: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ApplyModelReplacementCommandValidatorTests
{
    private readonly ApplyModelReplacementCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var command = new ApplyModelReplacementCommand(
            "meta-llama/llama-3.3-70b-instruct:free",
            "qwen/qwen-2.5-72b:free");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "replacement")]
    [InlineData(null, "replacement")]
    public void Validate_EmptyDeprecatedModelId_Fails(string? deprecatedModelId, string replacementModelId)
    {
        var command = new ApplyModelReplacementCommand(deprecatedModelId!, replacementModelId);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DeprecatedModelId);
    }

    [Theory]
    [InlineData("deprecated", "")]
    [InlineData("deprecated", null)]
    public void Validate_EmptyReplacementModelId_Fails(string deprecatedModelId, string? replacementModelId)
    {
        var command = new ApplyModelReplacementCommand(deprecatedModelId, replacementModelId!);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ReplacementModelId);
    }

    [Fact]
    public void Validate_SameModelIds_Fails()
    {
        var command = new ApplyModelReplacementCommand("same-model", "same-model");

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Replacement model must be different from the deprecated model");
    }

    [Fact]
    public void Validate_SameModelIdsCaseInsensitive_Fails()
    {
        var command = new ApplyModelReplacementCommand("Same-Model", "same-model");

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ModelIdTooLong_Fails()
    {
        var longModelId = new string('a', 201);

        var command = new ApplyModelReplacementCommand(longModelId, "replacement");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DeprecatedModelId);
    }
}
