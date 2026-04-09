using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class UpdateHandSlotCommandValidator : AbstractValidator<UpdateHandSlotCommand>
{
    private static readonly string[] ValidSlotTypes = ["toolkit", "game", "session", "ai"];
    private static readonly string[] ValidEntityTypes = ["toolkit", "game", "session", "agent"];

    private static readonly Dictionary<string, string[]> SlotEntityMap = new()
    {
        ["toolkit"] = ["toolkit"],
        ["game"]    = ["game"],
        ["session"] = ["session"],
        ["ai"]      = ["agent"]
    };

    public UpdateHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", ValidSlotTypes)}");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("EntityId is required");

        RuleFor(x => x.EntityType)
            .NotEmpty()
            .Must(t => ValidEntityTypes.Contains(t))
            .WithMessage($"EntityType must be one of: {string.Join(", ", ValidEntityTypes)}");

        RuleFor(x => x)
            .Must(x => SlotEntityMap.TryGetValue(x.SlotType, out var allowed) && allowed.Contains(x.EntityType))
            .WithMessage(x => $"EntityType '{x.EntityType}' is not valid for SlotType '{x.SlotType}'")
            .When(x => ValidSlotTypes.Contains(x.SlotType) && ValidEntityTypes.Contains(x.EntityType));
    }
}
