using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Validators;

/// <summary>
/// Validator for the root-level LogWorkflowErrorCommand (positional record variant).
/// </summary>
internal sealed class LogWorkflowErrorRootCommandValidator : AbstractValidator<LogWorkflowErrorCommand>
{
    public LogWorkflowErrorRootCommandValidator()
    {
        RuleFor(x => x.WorkflowId)
            .NotEmpty()
            .WithMessage("WorkflowId is required");

        RuleFor(x => x.ExecutionId)
            .NotEmpty()
            .WithMessage("ExecutionId is required");

        RuleFor(x => x.ErrorMessage)
            .NotEmpty()
            .MaximumLength(4000)
            .WithMessage("ErrorMessage is required (max 4000 chars)");
    }
}
