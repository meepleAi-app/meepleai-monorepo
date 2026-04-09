using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class ClearHandSlotCommandValidator : AbstractValidator<ClearHandSlotCommand>
{
    private static readonly HashSet<string> ValidSlotTypes = new(StringComparer.OrdinalIgnoreCase)
        { "toolkit", "game", "session", "ai" };

    public ClearHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", ValidSlotTypes)}");
    }
}
