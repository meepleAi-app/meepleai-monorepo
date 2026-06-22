using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validates <see cref="DeletePlayRecordCommand"/> (issue #2439).
/// </summary>
internal sealed class DeletePlayRecordCommandValidator : AbstractValidator<DeletePlayRecordCommand>
{
    public DeletePlayRecordCommandValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
