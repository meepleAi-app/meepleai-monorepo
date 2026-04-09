using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class ClearHandSlotCommandValidator : AbstractValidator<ClearHandSlotCommand>
{
    public ClearHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => HandSlotConstants.ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", HandSlotConstants.ValidSlotTypes)}");
    }
}
