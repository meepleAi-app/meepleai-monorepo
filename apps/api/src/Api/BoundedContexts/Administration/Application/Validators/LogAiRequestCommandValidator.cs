using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for LogAiRequestCommand.
/// Validates required fields for AI request telemetry logging.
/// </summary>
internal sealed class LogAiRequestCommandValidator : AbstractValidator<LogAiRequestCommand>
{
    public LogAiRequestCommandValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .WithMessage("Endpoint is required")
            .MaximumLength(500)
            .WithMessage("Endpoint must not exceed 500 characters");

        RuleFor(x => x.LatencyMs)
            .GreaterThanOrEqualTo(0)
            .WithMessage("LatencyMs must be non-negative");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required")
            .MaximumLength(50)
            .WithMessage("Status must not exceed 50 characters");

        RuleFor(x => x.Query)
            .MaximumLength(10000)
            .WithMessage("Query must not exceed 10000 characters")
            .When(x => x.Query != null);

        RuleFor(x => x.ResponseSnippet)
            .MaximumLength(10000)
            .WithMessage("ResponseSnippet must not exceed 10000 characters")
            .When(x => x.ResponseSnippet != null);

        RuleFor(x => x.TokenCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TokenCount must be non-negative")
            .When(x => x.TokenCount.HasValue);

        RuleFor(x => x.Confidence)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("Confidence must be between 0.0 and 1.0")
            .When(x => x.Confidence.HasValue);

        RuleFor(x => x.Model)
            .MaximumLength(200)
            .WithMessage("Model must not exceed 200 characters")
            .When(x => x.Model != null);

        RuleFor(x => x.ErrorMessage)
            .MaximumLength(5000)
            .WithMessage("ErrorMessage must not exceed 5000 characters")
            .When(x => x.ErrorMessage != null);
    }
}
