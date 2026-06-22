using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validates <see cref="GeneratePlayRecordShareTokenCommand"/> (#2437-2).
/// </summary>
internal sealed class GeneratePlayRecordShareTokenCommandValidator : AbstractValidator<GeneratePlayRecordShareTokenCommand>
{
    public GeneratePlayRecordShareTokenCommandValidator()
    {
        RuleFor(x => x.PlayRecordId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
