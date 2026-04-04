using Api.BoundedContexts.WorkflowIntegration.Application.Commands.WorkflowErrors;
using FluentValidation;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Validators;

internal sealed class LogWorkflowErrorCommandValidator : AbstractValidator<LogWorkflowErrorCommand>
{
    public LogWorkflowErrorCommandValidator()
    {
        RuleFor(x => x.WorkflowId)
            .NotEmpty()
            .WithMessage("WorkflowId is required");

        RuleFor(x => x.ErrorMessage)
            .NotEmpty()
            .MaximumLength(4000)
            .WithMessage("ErrorMessage is required (max 4000 chars)");

        RuleFor(x => x.RetryCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("RetryCount cannot be negative");

        RuleFor(x => x.StackTrace)
            .MaximumLength(8000)
            .When(x => x.StackTrace != null)
            .WithMessage("StackTrace cannot exceed 8000 characters");
    }
}
