using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class RejectDeleteRequestCommandValidator : AbstractValidator<RejectDeleteRequestCommand>
{
    public RejectDeleteRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("RequestId is required");

        RuleFor(x => x.RejectedBy)
            .NotEmpty().WithMessage("RejectedBy is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required");
    }
}
