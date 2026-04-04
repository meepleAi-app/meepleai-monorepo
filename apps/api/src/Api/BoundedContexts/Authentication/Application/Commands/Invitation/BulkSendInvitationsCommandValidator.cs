using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Validates BulkSendInvitationsCommand.
/// Ensures CSV content is provided and inviter ID is valid.
/// </summary>
internal sealed class BulkSendInvitationsCommandValidator : AbstractValidator<BulkSendInvitationsCommand>
{
    private const int MaxCsvLength = 1_048_576; // 1 MB

    public BulkSendInvitationsCommandValidator()
    {
        RuleFor(x => x.CsvContent)
            .NotEmpty()
            .WithMessage("CSV content is required")
            .MaximumLength(MaxCsvLength)
            .WithMessage($"CSV content must not exceed {MaxCsvLength} characters");

        RuleFor(x => x.InvitedByUserId)
            .NotEmpty()
            .WithMessage("Inviting user ID is required");
    }
}
