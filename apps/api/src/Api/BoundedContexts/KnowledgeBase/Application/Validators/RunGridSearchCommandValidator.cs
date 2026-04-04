using Api.BoundedContexts.KnowledgeBase.Application.GridSearch.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for RunGridSearchCommand.
/// </summary>
internal sealed class RunGridSearchCommandValidator : AbstractValidator<RunGridSearchCommand>
{
    public RunGridSearchCommandValidator()
    {
        RuleFor(x => x.DatasetPath)
            .MaximumLength(500)
            .When(x => x.DatasetPath != null)
            .WithMessage("DatasetPath cannot exceed 500 characters");

        RuleFor(x => x.MaxSamplesPerConfig)
            .GreaterThan(0)
            .When(x => x.MaxSamplesPerConfig.HasValue)
            .WithMessage("MaxSamplesPerConfig must be greater than 0");
    }
}
