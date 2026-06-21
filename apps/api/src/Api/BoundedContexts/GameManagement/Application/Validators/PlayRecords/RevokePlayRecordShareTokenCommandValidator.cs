using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validates <see cref="RevokePlayRecordShareTokenCommand"/> (#2437-2).
/// </summary>
internal sealed class RevokePlayRecordShareTokenCommandValidator : AbstractValidator<RevokePlayRecordShareTokenCommand>
{
    public RevokePlayRecordShareTokenCommandValidator()
    {
        RuleFor(x => x.PlayRecordId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
