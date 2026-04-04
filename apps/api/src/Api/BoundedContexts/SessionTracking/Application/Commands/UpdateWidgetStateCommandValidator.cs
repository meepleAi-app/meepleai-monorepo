using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class UpdateWidgetStateCommandValidator : AbstractValidator<UpdateWidgetStateCommand>
{
    public UpdateWidgetStateCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required");
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.WidgetType).NotEmpty().MaximumLength(100)
            .WithMessage("WidgetType is required and must be at most 100 characters");
        RuleFor(x => x.StateJson).NotEmpty().MaximumLength(50000)
            .WithMessage("StateJson is required and must be at most 50000 characters");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required");
    }
}
