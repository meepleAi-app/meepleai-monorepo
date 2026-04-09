using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class UpdateHandSlotCommandValidator : AbstractValidator<UpdateHandSlotCommand>
{
    public UpdateHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => HandSlotConstants.ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", HandSlotConstants.ValidSlotTypes)}");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("EntityId is required");

        RuleFor(x => x.EntityType)
            .NotEmpty()
            .Must(t => HandSlotConstants.ValidEntityTypes.Contains(t))
            .WithMessage($"EntityType must be one of: {string.Join(", ", HandSlotConstants.ValidEntityTypes)}");

        RuleFor(x => x)
            .Must(x => HandSlotConstants.SlotEntityMap.TryGetValue(x.SlotType, out var allowed)
                       && allowed.Contains(x.EntityType, StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"EntityType '{x.EntityType}' is not valid for SlotType '{x.SlotType}'")
            .When(x => HandSlotConstants.ValidSlotTypes.Contains(x.SlotType)
                       && HandSlotConstants.ValidEntityTypes.Contains(x.EntityType));
    }
}
