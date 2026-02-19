using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validator for CreateLiveSessionCommand.
/// Issue #4749: CQRS validation for live sessions.
/// </summary>
internal sealed class CreateLiveSessionCommandValidator : AbstractValidator<CreateLiveSessionCommand>
{
    public CreateLiveSessionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.GameName)
            .NotEmpty()
            .WithMessage("Game name is required")
            .MaximumLength(255)
            .WithMessage("Game name cannot exceed 255 characters");

        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithMessage("Invalid visibility value");

        RuleFor(x => x.GroupId)
            .NotEmpty()
            .When(x => x.Visibility == PlayRecordVisibility.Group)
            .WithMessage("GroupId is required when visibility is Group");

        RuleFor(x => x.AgentMode)
            .IsInEnum()
            .WithMessage("Invalid agent mode value");

        RuleFor(x => x.ScoringDimensions)
            .Must(dims => dims == null || dims.Count > 0)
            .WithMessage("Scoring dimensions cannot be empty if provided")
            .Must(dims => dims == null || dims.Count <= 10)
            .WithMessage("Maximum 10 scoring dimensions allowed");
    }
}
