using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for WithdrawShareRequestCommand.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
internal sealed class WithdrawShareRequestCommandValidator : AbstractValidator<WithdrawShareRequestCommand>
{
    public WithdrawShareRequestCommandValidator()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty()
            .WithMessage("ShareRequestId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
