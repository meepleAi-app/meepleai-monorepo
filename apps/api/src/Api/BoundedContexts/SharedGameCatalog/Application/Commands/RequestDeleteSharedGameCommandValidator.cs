using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class RequestDeleteSharedGameCommandValidator : AbstractValidator<RequestDeleteSharedGameCommand>
{
    public RequestDeleteSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("RequestedBy is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters");
    }
}
