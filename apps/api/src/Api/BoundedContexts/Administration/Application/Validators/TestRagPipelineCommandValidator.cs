using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for TestRagPipelineCommand.
/// Ensures pipeline definition and test query are provided.
/// </summary>
internal sealed class TestRagPipelineCommandValidator : AbstractValidator<TestRagPipelineCommand>
{
    public TestRagPipelineCommandValidator()
    {
        RuleFor(x => x.PipelineDefinition)
            .NotEmpty()
            .WithMessage("PipelineDefinition is required")
            .MaximumLength(50000)
            .WithMessage("PipelineDefinition must not exceed 50000 characters")
            .Must(BeValidJson)
            .WithMessage("PipelineDefinition must be valid JSON");

        RuleFor(x => x.TestQuery)
            .NotEmpty()
            .WithMessage("TestQuery is required")
            .MaximumLength(5000)
            .WithMessage("TestQuery must not exceed 5000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }

    private static bool BeValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
