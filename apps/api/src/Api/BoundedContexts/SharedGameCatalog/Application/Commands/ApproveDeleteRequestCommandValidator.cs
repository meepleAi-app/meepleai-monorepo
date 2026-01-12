using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class ApproveDeleteRequestCommandValidator : AbstractValidator<ApproveDeleteRequestCommand>
{
    public ApproveDeleteRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("RequestId is required");

        RuleFor(x => x.ApprovedBy)
            .NotEmpty().WithMessage("ApprovedBy is required");
    }
}
